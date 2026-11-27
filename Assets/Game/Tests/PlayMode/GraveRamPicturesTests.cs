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
    /// Work order 149, a look order's proof: pictures of the Grave Ram taken off screen in batch mode at the game's own camera, 1920 by
    /// 1080, the interface drawn into each by hand (<see cref="ImguiByHand"/>) as work order 145's are, in floor 1's elite room with its
    /// art: the Ram beside an ordinary brute; the Ram's strip at its tell's start and just before it runs; the Stomp's circle at its start
    /// and just before it strikes; the Grave Stones' first circle as it appears and the three just before the first bursts; the daze
    /// after a crash into the west wall; and its bar with its name (a crop of the top, twice its size). Nothing here judges them: they
    /// are looked at. Each picture's line in <c>149-log.txt</c>: where she and it stand, whether its whole body is on his screen.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_149 names a folder outside the project (otherwise ignored):
    ///   $env:ROGUELIKE_CAPTURE_149 = 'C:\Users\kriss\.claude-automation\runner\captures\149'
    ///   run-tests.ps1 PlayMode -Filter "GraveRamPicturesTests"
    /// </summary>
    public class GraveRamPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_149";
        const int W = 1920, H = 1080;
        const ulong From = 0x149A0UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _template;
        GeneratedLevels _levels;
        SkillBar _bar;
        EnemyBars _bars;
        CombatEffects _combat;
        string _folder;
        RenderTexture _rt;
        Texture2D _read;
        bool _uniqueBefore;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 149)");
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            _uniqueBefore = EliteValues.Unique;
            EliteValues.Unique = true;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _template = Object.FindAnyObjectByType<EnemyBrute>();
            _template.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _bars = EnemyBars.Ensure();
            _combat = CombatEffects.Ensure();
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "149-log.txt"), _log);
            _log.Clear();
            Time.captureDeltaTime = 0f;
            EliteValues.Unique = _uniqueBefore;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            if (_health != null) _health.Untouchable = false;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_levels != null)
            {
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _bars, _combat })
                if (m != null) Object.Destroy(m.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_read != null) Object.Destroy(_read);
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Draw()
        {
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = _rt;
                cam.aspect = (float)W / H;
                if (_bar != null) _bar.Tick();
                _bars.Tick();
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(_rt);
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _rt;
                _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                _read.Apply();
                RenderTexture.active = was;
            }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
        }

        void Save(string name, string what, GraveRamTestRoom r)
        {
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), _read.EncodeToPNG());
            EnemyGraveRam ram = r.Ram;
            _log.Add(string.Format(Inv, "{0}.png: {1}; she at {2}, the Ram at {3}, its state {4} {5}, wind-up left {6:0.00} s, its whole body on his screen {7}, dazed {8}",
                name, what, r.At(_player.Position), r.At(ram.transform.position), ram.State, ram.Attack != null ? ram.Attack.Name : "-", ram.WindupRemaining,
                ram.BodyOnScreen(), ram.IsDazed));
        }

        void SaveCrop(string name, int x0, int yTop, int w, int h, int zoom)
        {
            int y0 = H - yTop - h; // the texture's rows run from the bottom
            var crop = new Texture2D(w * zoom, h * zoom, TextureFormat.RGB24, false);
            try
            {
                var px = new Color32[w * zoom * h * zoom];
                Color32[] src = _read.GetPixels32();
                for (int y = 0; y < h * zoom; y++)
                    for (int x = 0; x < w * zoom; x++)
                        px[y * w * zoom + x] = src[(y0 + y / zoom) * W + x0 + x / zoom];
                crop.SetPixels32(px);
                crop.Apply();
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), crop.EncodeToPNG());
            }
            finally { Object.Destroy(crop); }
        }

        /// <summary>A crop twice its size round a world point as the picture's camera showed it.</summary>
        void CropRound(string name, Vector3 world)
        {
            Camera cam = Camera.main;
            cam.aspect = (float)W / H;
            Vector3 v = cam.WorldToViewportPoint(world);
            int cx = Mathf.RoundToInt(v.x * W), cy = Mathf.RoundToInt((1f - v.y) * H);
            const int Size = 320;
            SaveCrop(name, Mathf.Clamp(cx - Size / 2, 0, W - Size), Mathf.Clamp(cy - Size / 2, 0, H - Size), Size, Size, 2);
        }

        Vector3 Floor(GraveRamTestRoom r, double d, double s)
        {
            Assert.That(GraveRamTestRoom.OnFloor(r.World(d, s), 0.7f, out Vector3 at), Is.True);
            return at;
        }

        IEnumerator Settle(int frames = 12)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Pictures_TheRamBesideABrute_EachTellAtItsStartAndBeforeItsHit_TheDaze_AndItsBar()
        {
            Time.captureDeltaTime = GraveRamTestRoom.Step;
            if (Camera.main != null) Camera.main.aspect = (float)W / H;
            ulong seed = GraveRamTestRoom.ApseSeed(_levels.Values, From);
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; work order 149; {W}x{H}; floor 1 seed {LevelSeed.ToText(seed)}");
            GraveRamTestRoom r = null;
            yield return GraveRamTestRoom.Build(_levels, _template, _player, seed, x => r = x, wake: false);

            // 1. Beside an ordinary brute of floor 1 (asleep, both), she 6 m before them.
            Vector3 home = r.Ram.Home;
            r.ToFrame(home, out double hd, out double hs);
            Vector3 brutePlace = Floor(r, hd, hs + 3.0);
            Enemy brute = EnemyFactory.Make(EnemyKind.Brute, brutePlace, r.Level.transform, _template, "PictureBrute");
            _player.Teleport(Floor(r, hd - 12.0, hs + 1.5)); // outside the brute's 9 m while it settles into its idle pose
            yield return Settle(8);
            brute.enabled = false; // held in its idle pose, asleep
            r.Ram.enabled = false; // the same: the two side by side, still
            _player.Teleport(Floor(r, hd - 4.5, hs + 1.5));
            yield return Settle(4);
            Draw();
            Save("149-1-ram-beside-a-brute", "the Grave Ram (left) and an ordinary floor-1 brute (right), held still, she 4.5 m before them", r);
            Object.Destroy(brute.gameObject);
            r.Ram.enabled = true;
            _player.Teleport(Floor(r, 1.0, 0.0));
            yield return null;

            yield return r.Wake();

            // 2, 3. The Ram's strip, at its tell's start and just before it runs.
            Vector3 her = Floor(r, 6, -4), at = Floor(r, 6, 4);
            _player.Teleport(her);
            yield return r.Put(at);
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.State == EnemyState.Windup && r.Ram.Attack.Name == GraveRamValues.Ram, () => _player.Teleport(her));
            Assume.That(r.Ram.Attack != null && r.Ram.Attack.Name == GraveRamValues.Ram, Is.True, "a Ram");
            Draw();
            Save("149-2-ram-tell-start", "the Ram's strip at its tell's first frame, she 8 m off in it", r);
            SaveCrop("149-9-bar-crop", (W - 900) / 2, 0, 900, 150, 2);
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.WindupRemaining < 0.05f, () => _player.Teleport(her));
            Draw();
            Save("149-3-ram-tell-end", "the Ram's strip in its tell's last 0.05 s, leaning", r);
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.LastRamEnd != RamEnd.None, () => _player.Teleport(her));

            // 4, 5. The Stomp's circle.
            her = Floor(r, 6, -1.4);
            at = Floor(r, 6, 0);
            _player.Teleport(her);
            yield return r.Put(at);
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.State == EnemyState.Windup && r.Ram.Attack.Name == GraveRamValues.Stomp, () => _player.Teleport(her));
            Assume.That(r.Ram.Attack != null && r.Ram.Attack.Name == GraveRamValues.Stomp, Is.True, "a Stomp");
            Draw();
            Save("149-4-stomp-tell-start", "the Stomp's circle at its tell's first frame, she against it", r);
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.WindupRemaining < 0.05f, () => _player.Teleport(her));
            Draw();
            Save("149-5-stomp-tell-end", "the Stomp's circle in its tell's last 0.05 s", r);
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.State == EnemyState.Chase, () => _player.Teleport(her));

            // 6, 7. The Grave Stones, she behind the right pier.
            her = Floor(r, 10, 12.8);
            at = Floor(r, 10, 1.5);
            _player.Teleport(her);
            yield return r.Put(at);
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.Stones.Any(s => s.Pending), () => _player.Teleport(her));
            Assume.That(r.Ram.Stones.Any(s => s.Pending), Is.True, "a throw");
            Draw();
            Save("149-6-stones-first", "the Grave Stones' first circle as it appears at her feet, she behind the right pier", r);
            // She walks out a little each stone, so the three lie apart and can be seen.
            Vector3 south = GraveRamTestRoom.Flat(r.World(0, 12.8) - r.World(10, 12.8)).normalized;
            float firstBurst = r.Ram.Stones.Where(s => s.Pending).Min(s => s.BurstAt);
            yield return GraveRamTestRoom.Run(2f, () => Time.time >= firstBurst - 0.05f, () => _player.Steer(south * 0.5f));
            _player.Steer(Vector3.zero);
            Draw();
            Save("149-7-stones-before-the-first-bursts", "the stones' circles 0.05 s before the first bursts, she walking south", r);
            yield return GraveRamTestRoom.Run(2.5f, () => !r.Ram.Stones.Any(s => s.Pending));

            // 8. The daze after a crash into the west wall.
            her = Floor(r, 6, -11.5);
            at = Floor(r, 6, -3.5);
            _player.Teleport(her);
            yield return r.Put(at);
            int crashes = r.Ram.Crashes;
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.State == EnemyState.Windup && r.Ram.Attack.Name == GraveRamValues.Ram, () => _player.Teleport(her));
            yield return GraveRamTestRoom.Run(0.3f, null);
            _player.Teleport(Floor(r, 2.5, -11.5));
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.Crashes > crashes && r.Ram.CrashDazed);
            yield return GraveRamTestRoom.Run(0.5f, null);
            Draw();
            Save("149-8-crash-daze", "0.5 s into the daze after its Ram met the west wall, she stepped aside (round 2: its body the deep daze blue, its bar's line the daze's time left)", r);
            CropRound("149-8-crash-daze-crop", r.Ram.transform.position + Vector3.up * 1.4f);
            if (r.Ram.Look != null)
                _log.Add("  its parts in the daze: " + string.Join(", ", r.Ram.Look.Renderers.Where(x => x != null && x.sharedMaterial != null)
                                                                               .Select(x => x.name + " " + x.sharedMaterial.color + (x.enabled ? "" : " (hidden)"))));
            SaveCrop("149-10-bar-in-the-daze", (W - 900) / 2, 0, 900, 150, 2);
            yield return GraveRamTestRoom.Run(1.4f, null);
            Draw();
            Save("149-11-crash-daze-later", "1.9 s into the same daze (0.6 s left): the bar's line nearly drained", r);
            SaveCrop("149-11-bar-later-in-the-daze", (W - 900) / 2, 0, 900, 150, 2);
            Time.captureDeltaTime = 0f;
        }
    }
}
