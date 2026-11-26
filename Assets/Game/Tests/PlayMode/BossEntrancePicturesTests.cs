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
    /// Work order 130's pictures, taken without a window at 1920 by 1080 from the game's own camera, the interface drawn into them by
    /// hand (<see cref="ImguiByHand"/>, as work order 068's): the moment before the boss's entrance, the camera at its closest with
    /// the card for each line of the list, the boss's entrance at three moments, and the first frame of the fight. With each, where
    /// the boss stands on the picture (its body's bounds projected) against the card's top, and whether the card's words fit their
    /// boxes. It changes nothing in the game. Runs ONLY when ROGUELIKE_CAPTURE_130 names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE_130 = 'C:\Users\kriss\.claude-automation\runner\captures\130'
    ///   run-tests.ps1 PlayMode -Filter "BossEntrancePicturesTests"
    /// </summary>
    public class BossEntrancePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_130";
        const int W = 1920, H = 1080;
        const ulong Seed = 0x130B055UL; // BossEntrancePlayTests' floor 5
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        string _folder;
        string[] _linesBefore;
        bool _onBefore;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 130)");
            Directory.CreateDirectory(_folder);
            _linesBefore = BossEntranceLines.Lines;
            _onBefore = BossEntranceValues.On;
            BossEntranceValues.On = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _levels.Values = new LevelValues();
            _levels.ShowDevBox = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            if (_linesBefore != null) BossEntranceLines.Lines = _linesBefore;
            BossEntranceValues.On = _onBefore;
            GameSettings.Unload();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            return level.ToWorld(p);
        }

        GeneratedLevel _level;
        EnemyBoss _boss;
        LevelRoom _room;
        LevelDoor _door;

        IEnumerator Floor5()
        {
            _practiceEnemy.enabled = true;
            _level = _levels.Build(Seed, 5);
            _practiceEnemy.enabled = false;
            yield return Frames(2);
            _boss = _level.Boss as EnemyBoss;
            _room = _level.Layout.ExitRoom;
            _door = LevelGeometry.EntryDoor(_level.Layout, _room);
            _player.Teleport(InFrom(_level, _room, _door, 1.0));
            for (int i = 0; i < 30 && _levels.CurrentRoom != _room; i++) yield return null;
            yield return Frames(30); // the room's light comes up as it is shown
        }

        IEnumerator Begin()
        {
            _player.Teleport(InFrom(_level, _room, _door, 5.5));
            for (int i = 0; i < 60 && !_levels.EntranceHolds; i++) yield return null;
            Assert.That(_levels.EntranceHolds, Is.True, "the entrance began");
        }

        IEnumerator At(float seconds)
        {
            _levels.HoldEntranceAt(seconds);
            yield return Frames(3);
        }

        /// <summary>The camera into a picture of 1920 by 1080, the interface drawn into it by hand; what was wrong is measured beside it.</summary>
        IEnumerator Picture(string name, string what)
        {
            yield return null;
            Camera cam = Camera.main;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = rt;
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                _log.Add(name + ".png: " + what + "; " + Measure(cam) + "; drew " + drew);
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        /// <summary>The boss's body on the picture (px from the top), the card's top, the words against their boxes.</summary>
        string Measure(Camera cam)
        {
            var parts = new List<string>();
            if (_boss != null)
            {
                Renderer[] rs = _boss.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is LineRenderer)).ToArray();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 v = cam.WorldToViewportPoint(c);
                        minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                    }
                    Vector3 head = cam.WorldToViewportPoint(_boss.transform.position + Vector3.up * Mathf.Max(0f, _boss.ModelHeight));
                    Vector3 feet = cam.WorldToViewportPoint(_boss.transform.position);
                    // The renderers' bounds are loose (the skinned meshes' and the toon look's own): in frame is asked of its head and feet, a body's width either side.
                    bool InView(Vector3 p) => p.z > 0f && p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;
                    Vector3 side = cam.transform.right * 1.2f;
                    Vector3 top = _boss.transform.position + Vector3.up * Mathf.Max(0f, _boss.ModelHeight);
                    bool inFrame = InView(cam.WorldToViewportPoint(top - side)) && InView(cam.WorldToViewportPoint(top + side))
                                   && InView(cam.WorldToViewportPoint(_boss.transform.position - side)) && InView(cam.WorldToViewportPoint(_boss.transform.position + side));
                    parts.Add(string.Format(Inv, "the boss's renderers' bounds (loose) on the picture x {0:0}..{1:0} px, y {2:0}..{3:0} px from the top ({4}); its feet at y {5:0}, the top of its head at y {6:0} px: {7:0} px tall ({8:0.0} % of the height)",
                                            minX * W, maxX * W, (1f - maxY) * H, (1f - minY) * H, inFrame ? "its head and feet and a body's width either side in frame" : "NOT ALL IN FRAME",
                                            (1f - feet.y) * H, (1f - head.y) * H, (head.y - feet.y) * H, (head.y - feet.y) * 100f));
                    if (_levels.EntranceCardShows > 0f)
                    {
                        float scale = InterfaceRules.Scale(H, InterfaceStyle.V);
                        UiBox whole = BossEntranceRules.Whole(W / scale, _levels.Entrance, InterfaceStyle.V);
                        float cardTop = whole.Y * scale;
                        parts.Add(string.Format(Inv, "the card's top at y {0:0} px: {1}", cardTop,
                                                (1f - minY) * H <= cardTop ? "the boss's whole body above it" : (1f - head.y) * H < cardTop - 40f ? "its head clear above it, its bounds reach " + ((1f - minY) * H - cardTop).ToString("0", Inv) + " px under its top" : "THE CARD OVER THE BOSS'S HEAD"));
                    }
                }
            }
            if (_levels.EntranceCardShows > 0f)
            {
                BossEntranceValues e = _levels.Entrance;
                float scale = InterfaceRules.Scale(H, InterfaceStyle.V);
                float viewWidth = W / scale;
                UiBox plate = BossEntranceRules.NamePlate(viewWidth, e), line = BossEntranceRules.LineBox(viewWidth, e);
                Vector2 name = InterfaceStyle.Measure(_levels.EntranceName, InterfaceStyle.Text(true, e.NameSize, TextAnchor.MiddleLeft));
                Vector2 words = InterfaceStyle.Measure(_levels.EntranceLine, InterfaceStyle.Text(false, e.LineSize, TextAnchor.MiddleCenter));
                float nameRoom = plate.W - e.NamePad - 24f;
                parts.Add(string.Format(Inv, "the name {0:0} by {1:0} layout px in {2:0} by {3:0} ({4}); the line {5:0} by {6:0} in {7:0} by {8:0} ({9}); fonts {10} / {11}",
                                        name.x, name.y, nameRoom, plate.H - 16f, name.x <= nameRoom && name.y <= plate.H ? "fits" : "DOES NOT FIT",
                                        words.x, words.y, line.W, line.H, words.x <= line.W && words.y <= line.H + 4f ? "fits" : "DOES NOT FIT",
                                        InterfaceStyle.Heavy != null ? InterfaceStyle.Heavy.name : "NO HEAVY FONT", InterfaceStyle.Light != null ? InterfaceStyle.Light.name : "NO LIGHT FONT"));
            }
            Camera c2 = cam;
            parts.Add(string.Format(Inv, "camera at {0:F2} looking {1:F1} degrees down, field of view {2:F1}, {3:F2} m from the boss", c2.transform.position, c2.transform.eulerAngles.x, c2.fieldOfView,
                                    _boss != null ? (c2.transform.position - _boss.transform.position).magnitude : 0f));
            return string.Join("; ", parts);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheEntrance_InPictures()
        {
            BossEntranceValues v = new BossEntranceValues();
            string[] lines = (string[])_linesBefore.Clone();
            float hold = v.TravelInSeconds;
            _log.Add($"{DateTime.Now:HH:mm:ss} work order 130, seed {LevelSeed.ToText(Seed)} floor 5, {W}x{H}, batch {Application.isBatchMode}, {SystemInfo.graphicsDeviceType}");

            // 1. The moment before: she a metre inside the boss room's door, the play camera.
            BossEntranceLines.Lines = new[] { lines[0] };
            yield return Floor5();
            yield return Picture("01-before-the-entrance", "she a metre inside the boss room's door, short of the point: the play camera");

            // 2. The entrance at three moments (the first line's card with them), then the closest with the card.
            yield return Begin();
            yield return At(hold * 0.5f);
            yield return Picture("02-travel-half-way", "half way of the camera's travel to the boss (0.45 s)");
            yield return At(hold + 0.35f);
            yield return Picture("03a-entrance-turning", "the entrance, 0.35 s into the hold: it turns toward her");
            yield return At(hold + v.CrouchFrom + v.CrouchSeconds - 0.01f);
            yield return Picture("03b-entrance-gathers", "the entrance at its crouch (its height " + v.CrouchSquash.ToString(Inv) + " of its own)");
            yield return At(hold + v.CrouchFrom + v.CrouchSeconds + v.LeapSeconds * 0.5f);
            yield return Picture("03c-entrance-springs", "the entrance at the top of its spring (" + v.LeapHeight.ToString(Inv) + " m)");
            yield return At(hold + 2.0f);
            yield return Picture("04-card-line1", "the camera at its closest, the card with line 1: " + lines[0]);
            yield return At(hold + v.HoldSeconds + v.TravelBackSeconds * 0.5f);
            yield return Picture("03d-entrance-turns-to-her", "half way of the travel back: it turns from the camera to face her");
            _levels.ReleaseEntranceTime();
            _levels.SkipEntrance();
            yield return Frames(2);
            yield return Picture("06-first-frame-of-the-fight", "the first frame of the fight after the entrance: the play camera");

            // 3. The card with every other line of the list.
            for (int i = 1; i < lines.Length; i++)
            {
                BossEntranceLines.Lines = new[] { lines[i] };
                yield return Floor5();
                yield return Begin();
                yield return At(hold + 2.0f);
                yield return Picture("05-card-line" + (i + 1), "the camera at its closest, the card with line " + (i + 1) + ": " + lines[i]);
                _levels.ReleaseEntranceTime();
                _levels.SkipEntrance();
                yield return Frames(2);
            }
            BossEntranceLines.Lines = _linesBefore;
            _levels.BackToPracticeRoom();
            yield return null;
        }
    }
}
