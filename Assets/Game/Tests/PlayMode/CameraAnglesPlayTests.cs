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
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 121's camera angles in the ControlRoom scene: each of the four puts the game's camera where its numbers say
    /// (pitch, field of view, distance; her height on the screen as the rule works it out), at every step of the camera
    /// distance; "Ours" is today's camera field for field (the feel's own pitch, field of view and stepped distance, by the very
    /// formula the rig used before); the pause menu's line stands just above the camera's kick, steps Ours, Diablo, League,
    /// PoE and back (and back the other way), is kept as the distance's step is, and the camera follows it in the frame it
    /// changes; with the order's switch off there is no line and the camera is ours whatever is stored; the session log tells
    /// the angle when it opens and at each change, with its name and its three numbers. Writes the numbers ("CAMERAANGLES").
    /// </summary>
    public class CameraAnglesPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Her height for the on-screen measure: the cave's (CaveValues.HerHeight), as the rule's.</summary>
        static readonly float HerHeight = new CaveValues().HerHeight;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        int? _angleBefore, _distanceBefore;
        PlaySessionRecorder _recorder;
        string _recordings;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            // The developer's own settings are in the same PlayerPrefs: kept aside and put back.
            _angleBefore = PlayerPrefs.HasKey(GameSettings.CameraAngleKey) ? PlayerPrefs.GetInt(GameSettings.CameraAngleKey) : (int?)null;
            _distanceBefore = PlayerPrefs.HasKey(GameSettings.CameraDistanceKey) ? PlayerPrefs.GetInt(GameSettings.CameraDistanceKey) : (int?)null;
            CameraAngles.On = true;
            GameSettings.SetCameraAngleStep(CameraAngles.Ours, false);
            GameSettings.SetCameraDistanceStep(0, false);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure(); // the pause menu needs it
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_recordings != null && Directory.Exists(_recordings)) Directory.Delete(_recordings, true);
            _recordings = null;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            CameraAngles.On = true;
            GameSettings.Unload();
            if (_angleBefore.HasValue) PlayerPrefs.SetInt(GameSettings.CameraAngleKey, _angleBefore.Value);
            else PlayerPrefs.DeleteKey(GameSettings.CameraAngleKey);
            if (_distanceBefore.HasValue) PlayerPrefs.SetInt(GameSettings.CameraDistanceKey, _distanceBefore.Value);
            else PlayerPrefs.DeleteKey(GameSettings.CameraDistanceKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static CameraRig Rig => Camera.main.GetComponent<CameraRig>();

        static FeelValues Feel(CameraRig rig) => rig.Settings != null ? rig.Settings.Values : new FeelValues();

        static Vector3 Focus(CameraRig rig) => new Vector3(rig.Target.position.x, 0f, rig.Target.position.z);

        /// <summary>Her height on the screen as a share of its height, measured through the game's camera: her feet at the point it looks at, her head HerHeight over it.</summary>
        static double MeasuredShare(Camera cam, Vector3 focus)
        {
            Vector3 feet = cam.WorldToScreenPoint(focus), head = cam.WorldToScreenPoint(focus + Vector3.up * HerHeight);
            return (head.y - feet.y) / cam.pixelHeight;
        }

        [UnityTest]
        public IEnumerator EachAngle_PutsTheCameraWhereItsNumbersSay_AtEveryDistanceStep_AndOursIsTodaysCameraFieldForField()
        {
            Camera cam = Camera.main;
            CameraRig rig = Rig;
            Assert.That(rig, Is.Not.Null);
            FeelValues feel = Feel(rig);
            var lines = new List<string>();
            for (int step = 0; step < CameraDistanceSteps.Count; step++)
            {
                GameSettings.SetCameraDistanceStep(step, false);
                // Ours: field for field the camera as the rig placed it before work order 121.
                GameSettings.SetCameraAngleStep(CameraAngles.Ours, false);
                yield return Frames(2);
                Vector3 focus = Focus(rig);
                Quaternion before = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
                Vector3 beforeAt = focus - before * Vector3.forward * GameSettings.CameraDistance(feel.CameraDistance);
                Assert.That(cam.fieldOfView, Is.EqualTo(feel.FieldOfView), "ours: the feel's own field of view, step " + step);
                // The rotation the transform keeps is normalised by it (a last bit off Quaternion.Euler's, before this order as
                // after); the numbers the rig hands it are the feel's own, bit for bit (CameraAnglesTests, the rule).
                Assert.That(Quaternion.Angle(cam.transform.rotation, before), Is.LessThan(1e-3f), "ours: the feel's own pitch");
                Assert.That(Mathf.Abs(cam.transform.rotation.x - before.x) + Mathf.Abs(cam.transform.rotation.w - before.w), Is.LessThan(1e-6f));
                Assert.That(cam.transform.position.x, Is.EqualTo(beforeAt.x), "ours: where it stood, step " + step);
                Assert.That(cam.transform.position.y, Is.EqualTo(beforeAt.y));
                Assert.That(cam.transform.position.z, Is.EqualTo(beforeAt.z));

                for (int a = 0; a < CameraAngles.Count; a++)
                {
                    GameSettings.SetCameraAngleStep(a, false);
                    yield return Frames(2);
                    focus = Focus(rig);
                    CameraAngle want = GameSettings.CameraNow(feel);
                    Assert.That(want.Name, Is.EqualTo(CameraAngles.Names[a]));
                    if (a != CameraAngles.Ours)
                    {
                        Assert.That(want.Pitch, Is.EqualTo(CameraAngles.Presets[a].Pitch), "the angle's own pitch");
                        Assert.That(want.FieldOfView, Is.EqualTo(CameraAngles.Presets[a].FieldOfView));
                        Assert.That(want.Distance, Is.EqualTo(CameraAngles.Presets[a].Distance * CameraDistanceSteps.Distance(step, feel.CameraDistance) / feel.CameraDistance).Within(1e-4f),
                                    "its own distance scaled as the step scales the feel's");
                    }
                    string at = CameraAngles.Names[a] + " step " + step;
                    Assert.That(cam.fieldOfView, Is.EqualTo(want.FieldOfView).Within(1e-5f), at + ": field of view");
                    Assert.That(Vector3.Angle(cam.transform.forward, Quaternion.Euler(want.Pitch, 0f, 0f) * Vector3.forward), Is.LessThan(0.01f), at + ": pitch");
                    Assert.That(cam.transform.forward.x, Is.EqualTo(0f).Within(1e-6f), at + ": the yaw stays ours (no turn round her)");
                    Assert.That(Vector3.Distance(cam.transform.position, focus), Is.EqualTo(want.Distance).Within(1e-3f), at + ": distance from the point under her");
                    Assert.That(cam.transform.position.y, Is.EqualTo(want.Distance * Mathf.Sin(want.Pitch * Mathf.Deg2Rad)).Within(1e-3f), at + ": height");
                    Vector3 looks = cam.transform.position + cam.transform.forward * want.Distance;
                    Assert.That(Vector3.Distance(looks, focus), Is.LessThan(1e-3f), at + ": it looks at the point under her");
                    double share = MeasuredShare(cam, focus), rule = CameraAngles.HeightShare(want.Pitch, want.FieldOfView, want.Distance, HerHeight);
                    Assert.That(share, Is.EqualTo(rule).Within(rule * 0.01), at + ": her height on the screen as the rule works it out");
                    lines.Add(string.Format(Inv, "{0}: pitch {1:0.##}, field of view {2:0.##}, distance {3:0.##} m, camera {4:0.##} m up; her 1.8 m {5:0.0} % of the screen's height measured ({6:0.0} % by the rule), {7:0} px at 1080 rows",
                                            at, want.Pitch, want.FieldOfView, want.Distance, cam.transform.position.y, 100.0 * share, 100.0 * rule, 1080.0 * share));
                }
            }
            foreach (string line in lines) TestContext.WriteLine("CAMERAANGLES " + line);
        }

        [UnityTest]
        public IEnumerator ThePauseMenusLine_AboveTheKick_StepsTheFourAndBack_IsKept_AndTheCameraFollowsInTheFrameItChanges()
        {
            Camera cam = Camera.main;
            CameraRig rig = Rig;
            FeelValues feel = Feel(rig);
            RunHud hud = RunHud.Ensure();
            yield return null;
            GameSettings.SetCameraAngleStep(CameraAngles.Ours, false); // whatever this machine has stored: the test starts at ours
            GameSettings.SetCameraDistanceStep(0, false);
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            Assert.That(hud.Escape(), Is.True, "paused");
            yield return null;
            List<RunHud.MenuLine> lines = hud.MenuLines.ToList();
            int angle = lines.IndexOf(RunHud.MenuLine.CameraAngle), kick = lines.IndexOf(RunHud.MenuLine.CameraKick);
            Assert.That(angle, Is.GreaterThanOrEqualTo(0), "the line is in the menu");
            Assert.That(kick, Is.EqualTo(angle + 1), "just above the camera's kick");
            Assert.That(lines[kick + 1], Is.EqualTo(RunHud.MenuLine.CameraDistance), "the kick and the distance side by side as before");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(hud.MenuText(RunHud.MenuLine.CameraAngle), Is.EqualTo("Camera angle:  Ours"), "ours by default");

            // A watcher whose LateUpdate runs after every other: what the camera was at the end of each frame's LateUpdates.
            var watcher = new GameObject("CameraAnglesWatcher").AddComponent<LateCameraWatcher>();
            watcher.Cam = cam;
            var seen = new List<string>();
            for (int i = 0; i < CameraAngles.Count; i++)
            {
                int frame = Time.frameCount;
                Assert.That(hud.PickMenuLine(RunHud.MenuLine.CameraAngle), Is.True); // the test's code runs after the Updates, before the LateUpdates
                seen.Add(hud.MenuText(RunHud.MenuLine.CameraAngle));
                yield return null;
                CameraAngle want = GameSettings.CameraNow(feel);
                Assert.That(watcher.FrameOf(frame, out float fov, out Vector3 at), Is.True, "the watcher saw the frame it changed in");
                Assert.That(fov, Is.EqualTo(want.FieldOfView).Within(1e-5f), seen[i] + ": the camera followed in the same frame");
                Assert.That(Vector3.Distance(at, Focus(rig)), Is.EqualTo(want.Distance).Within(1e-3f), seen[i] + ": and stood at its distance in that frame");
                Assert.That(PlayerPrefs.GetInt(GameSettings.CameraAngleKey, -1), Is.EqualTo(GameSettings.CameraAngleStep), "kept");
                yield return null;
            }
            Assert.That(seen, Is.EqualTo(new[] { "Camera angle:  Diablo", "Camera angle:  League", "Camera angle:  PoE", "Camera angle:  Ours" }), "Enter steps on, round to ours");
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.CameraAngle, -1), Is.True);
            Assert.That(GameSettings.CameraAngleStep, Is.EqualTo(CameraAngles.PathOfExile), "the left arrow steps back, round to PoE");
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.CameraAngle, 1), Is.True);
            Assert.That(GameSettings.CameraAngleStep, Is.EqualTo(CameraAngles.Ours), "and the right arrow on again");
            Assert.That(() => { hud.MenuText(RunHud.MenuLine.CameraAngle); }, AllocExt.AllocatingGCMemory(Is.Not), "the line's words are made once");

            // Kept between starts, as the distance's step is.
            GameSettings.SetCameraAngleStep(CameraAngles.League);
            GameSettings.Unload();
            Assert.That(GameSettings.CameraAngleStep, Is.EqualTo(CameraAngles.Ours), "forgotten until it is read");
            GameSettings.Load();
            Assert.That(GameSettings.CameraAngleStep, Is.EqualTo(CameraAngles.League), "read back from PlayerPrefs");

            // The distance's line on top of an angle: its steps scale League's own distance.
            Assert.That(hud.PickMenuLine(RunHud.MenuLine.CameraDistance), Is.True);
            yield return Frames(2);
            float scaled = CameraAngles.Presets[CameraAngles.League].Distance * CameraDistanceSteps.Closer[0] / feel.CameraDistance;
            Assert.That(Vector3.Distance(cam.transform.position, Focus(rig)), Is.EqualTo(scaled).Within(1e-3f), "League's distance at the distance's first closer step");
            hud.Escape();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithTheSwitchOff_NoLine_AndTheCameraIsOursWhateverIsStored()
        {
            Camera cam = Camera.main;
            CameraRig rig = Rig;
            FeelValues feel = Feel(rig);
            CameraAngles.On = false;
            GameSettings.SetCameraAngleStep(CameraAngles.Diablo, false);
            yield return Frames(2);
            Quaternion before = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            Vector3 beforeAt = Focus(rig) - before * Vector3.forward * GameSettings.CameraDistance(feel.CameraDistance);
            Assert.That(cam.fieldOfView, Is.EqualTo(feel.FieldOfView));
            Assert.That(cam.transform.position, Is.EqualTo(beforeAt), "today's camera with Diablo stored");
            RunHud hud = RunHud.Ensure();
            yield return null;
            Assert.That(hud.Escape(), Is.True);
            yield return null;
            Assert.That(hud.MenuLines, Has.No.Member(RunHud.MenuLine.CameraAngle), "no line with the switch off");
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.CameraAngle, 1), Is.True, "the call itself still steps the stored value");
            hud.Escape();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSessionLog_TellsTheAngle_WhenItOpens_AndAtEachChange_WithItsNameAndItsThreeNumbers()
        {
            FeelValues feel = Feel(Rig);
            _recordings = Path.Combine(Path.GetTempPath(), "CameraAnglesPlayTests-rec-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_recordings);
            yield return Frames(3);
            Assert.That(_recorder.Player, Is.SameAs(_player));
            GameSettings.SetCameraAngleStep(CameraAngles.Diablo, false);
            yield return Frames(2);
            GameSettings.SetCameraDistanceStep(2, false);
            yield return Frames(2);
            GameSettings.SetCameraAngleStep(CameraAngles.PathOfExile, false);
            yield return Frames(5); // nothing changes: nothing more is written
            List<SessionEvent> angles = _recorder.Events.Where(e => e.Type == CameraAngleEvents.Change).ToList();
            Assert.That(angles.Select(e => e.Name), Is.EqualTo(new[] { "Ours", "Diablo", "Diablo", "PoE" }), "when the log opens, then each change of the angle or the distance's step");
            SessionEvent ours = angles[0], diablo = angles[1], stepped = angles[2], poe = angles[3];
            Assert.That(ours.ExtraValue("pitch"), Is.EqualTo(feel.CameraPitch.ToString("0.####", Inv)), "ours: the feel's own");
            Assert.That(ours.ExtraValue("field_of_view"), Is.EqualTo(feel.FieldOfView.ToString("0.####", Inv)));
            Assert.That(ours.ExtraValue("distance"), Is.EqualTo(feel.CameraDistance.ToString("0.####", Inv)));
            CameraAngle d = CameraAngles.Presets[CameraAngles.Diablo];
            Assert.That(diablo.ExtraValue("pitch"), Is.EqualTo(d.Pitch.ToString("0.####", Inv)));
            Assert.That(diablo.ExtraValue("field_of_view"), Is.EqualTo(d.FieldOfView.ToString("0.####", Inv)));
            Assert.That(diablo.ExtraValue("distance"), Is.EqualTo(d.Distance.ToString("0.####", Inv)));
            Assert.That(diablo.ExtraValue("distance_step"), Is.EqualTo("0"));
            Assert.That(stepped.ExtraValue("distance_step"), Is.EqualTo("2"));
            Assert.That(stepped.ExtraValue("distance"), Is.EqualTo(d.Distance.ToString("0.####", Inv)), "the angle's own distance");
            Assert.That(stepped.ExtraValue("distance_in_use"), Is.EqualTo((d.Distance * (CameraDistanceSteps.Closer[1] / feel.CameraDistance)).ToString("0.####", Inv)), "and the one in use");
            Assert.That(poe.ExtraValue("pitch"), Is.EqualTo(CameraAngles.Presets[CameraAngles.PathOfExile].Pitch.ToString("0.####", Inv)));
            _recorder.StopRecording();
            string log = File.ReadAllText(_recorder.LogPath);
            Assert.That(log, Does.Contain("\"type\":\"camera_angle\",\"name\":\"PoE\""), "in the log's own lines");
        }
    }

    /// <summary>For <see cref="CameraAnglesPlayTests"/>: the camera's field of view and place at the end of each frame's LateUpdates (it runs after every other).</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class LateCameraWatcher : MonoBehaviour
    {
        public Camera Cam;
        int _frame = -1;
        float _fov;
        Vector3 _at;

        void LateUpdate()
        {
            if (Cam == null) return;
            _frame = Time.frameCount;
            _fov = Cam.fieldOfView;
            _at = Cam.transform.position;
        }

        /// <summary>What it saw at the end of frame <paramref name="frame"/>'s LateUpdates; false if that was not the last frame it saw.</summary>
        public bool FrameOf(int frame, out float fov, out Vector3 at)
        {
            fov = _fov;
            at = _at;
            return _frame == frame;
        }
    }
}
