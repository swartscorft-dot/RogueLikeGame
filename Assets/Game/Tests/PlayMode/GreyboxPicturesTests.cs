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
    /// Work order 084: pictures of the greybox view, taken without a window, so the producer and the developer can see that it
    /// reads (one small round). Runs ONLY when the environment variable ROGUELIKE_CAPTURE names a folder outside the project
    /// (otherwise it is skipped: no ordinary suite takes a picture or leaves a file):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\084\round1'
    ///   run-tests.ps1 PlayMode -Filter "GreyboxPicturesTests"
    ///
    /// The game's own camera renders into a texture as it follows her (the interface, and so the developer's box with the
    /// legend, is not drawn: IMGUI needs a window); for the lab's rooms also a camera straight above, looking down on the whole
    /// room, as a plan. Each place is taken with the view off and on.
    /// </summary>
    public class GreyboxPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const int W = 1280, H = 720;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 084)");
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures.txt"), _log);
            _log.Clear();
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        void Save(Camera cam, string name, string what)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                _log.Add(name + ".png: " + what);
            }
            catch (Exception e) { _log.Add(name + " failed: " + e.Message); }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        IEnumerator OffAndOn(string name, string what, Func<Camera> camera)
        {
            foreach (bool on in new[] { false, true })
            {
                GameSettings.SetGreybox(on, keep: false);
                yield return Frames(4); // (WaitForEndOfFrame is never evoked in batch mode: the camera is rendered by hand)
                Save(camera(), name + (on ? "-greybox" : "-art"), what + (on ? ", the greybox view on" : ", the view off (the game as he plays it)"));
            }
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(2);
        }

        /// <summary>A camera straight above the room, orthographic, looking down on the whole of it: a plan.</summary>
        Camera Above(GeneratedLevel level, LevelRoom room, Camera like)
        {
            var go = new GameObject("GreyboxPictureCamera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(like);
            cam.orthographic = true;
            float cs = level.Layout.CellSize;
            float w = (room.Rect.Width + 2) * cs, h = (room.Rect.Height + 5) * cs;
            cam.orthographicSize = Mathf.Max(h * 0.5f, w * 0.5f * H / W);
            LevelPoint middle = new LevelPoint((room.Rect.X + room.Rect.Width * 0.5) * cs, (room.Rect.Y + room.Rect.Height * 0.5 - 1.5) * cs);
            Vector3 at = level.ToWorld(middle);
            go.transform.SetPositionAndRotation(at + Vector3.up * 60f, Quaternion.Euler(90f, 0f, 0f));
            cam.farClipPlane = 200f;
            cam.enabled = false;
            return cam;
        }

        static Vector3 OpenSpot(GeneratedLevel level, LevelRoom room, double depth)
        {
            LevelLayout l = level.Layout;
            double cs = l.CellSize;
            LevelPoint m = room.Middle(cs);
            var p = new LevelPoint(m.X, room.Rect.Y * cs + depth);
            for (int k = 0; k < room.Rect.Width && l.RoomAt(p.X, p.Y) != room; k++) p = new LevelPoint(m.X + (k % 2 == 0 ? 1 : -1) * (k / 2 + 1) * cs, p.Y);
            return level.ToWorld(p);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheGreyboxView_OffAndOn()
        {
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 084's round");
            Camera game = Camera.main;

            // A floor of a run: its first templated combat room, from the game's camera, she a few metres in.
            _levels.Values = new LevelValues();
            GeneratedLevel floor = _levels.StartRun(0x0840A0A0UL);
            yield return Frames(3);
            LevelRoom combat = floor.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && r.Shape == RoomShape.Template);
            _player.Teleport(OpenSpot(floor, combat, 3.0));
            yield return Frames(4);
            yield return OffAndOn("floor-" + combat.Letter + combat.Order, "a floor of a run (seed " + floor.Layout.SeedText + "), room " + combat.Letter + combat.Order + ", "
                                  + GreyboxRules.RoomWords(combat, floor.Layout.CellSize) + ", from the game's camera, she 3 m in from its door", () => game);
            _levels.BackToPracticeRoom();
            yield return Frames(3);

            // The room lab: an elite room, an ordinary room of the second set, the exit room of floor 5 (the act boss), each from
            // her place in the passage (the game's camera) and from straight above.
            List<LabEntry> entries = RoomLab.Entries(_levels.Values);
            var picks = new List<(LabEntry entry, int mix)>
            {
                // Work order 090: the presets one a floor in order; the floors these pictures were of (5, 3, 5; indices 2, 1, 2 before).
                (entries.First(e => e.Kind == LabKind.Elite && e.Template != null), 4),
                (entries.First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Since >= RoomTemplateLibrary.MoreSince), 2),
                (entries.First(e => e.Kind == LabKind.Exit && e.Template != null), 4),
            };
            foreach ((LabEntry entry, int mix) in picks)
            {
                GeneratedLevel lab = _levels.StartLab(entry, mix, 0x084084UL);
                Assert.That(lab, Is.Not.Null, entry.ToString());
                yield return Frames(4);
                LevelRoom room = lab.Layout.Rooms[1];
                string what = "the room lab: " + RoomLab.Describe(entry, RoomLab.Mixes[mix], 0x084084UL) + ", " + GreyboxRules.RoomWords(room, lab.Layout.CellSize)
                              + string.Format(Inv, ", built in {0:0} ms", _levels.LabBuiltMs);
                string name = "lab-" + RoomLab.Word(entry.Kind) + "-" + entry.Name.Replace(' ', '_');
                _player.Teleport(OpenSpot(lab, room, 2.0));
                yield return Frames(4);
                yield return OffAndOn(name + "-door", what + ", from the game's camera, she just inside the door", () => game);
                Camera above = Above(lab, room, game);
                yield return OffAndOn(name + "-plan", what + ", from straight above (a plan; north up, the door at the bottom)", () => above);
                Object.Destroy(above.gameObject);
                _log.Add(LevelMap.Draw(lab.Layout));
            }
            _levels.BackToPracticeRoom();
            yield return Frames(2);
        }
    }
}
