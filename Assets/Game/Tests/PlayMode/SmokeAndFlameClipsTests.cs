using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 126, a look order's proof in motion: Powder Keg and Smoke Round played in the practice room for the whole life of
    /// their effects, every frame at a steady 30 a second of game time, at 1920 by 1080 from two cameras (the game's own, following
    /// her as it does; and one close, the game's pitch, 13 m from the action), 123's look (<see cref="SkillLookValues.ToonLook"/>
    /// off, its own numbers) and this order's. Also: the smoke with her and an enemy standing in it; the keg with MSAA 4x forced on
    /// the capture (125 turns anti-aliasing on for the whole picture); the keg without its starburst. The frames go to a folder
    /// for ffmpeg; nothing here judges them: they are looked at.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_126 names a folder outside the project (otherwise it is ignored):
    ///   $env:ROGUELIKE_CAPTURE_126 = 'C:\...\frames126'
    ///   run-tests.ps1 PlayMode -Filter "SmokeAndFlameClipsTests"
    /// </summary>
    public class SmokeAndFlameClipsTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_126";
        const int W = 1920, H = 1080;
        const float Step = 1f / 30f;     // s of game time a frame: a steady 30 frames a second
        const float CloseDistance = 13f; // m, as 123's pictures
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        CombatEffects _combat;
        EnemyBrute _brute;
        string _folder;
        Vector3 _stand, _ahead, _side, _focus;
        readonly Dictionary<string, Damageable> _dummies = new Dictionary<string, Damageable>();
        readonly List<string> _log = new List<string>();
        RenderTexture _rt, _plain;
        Texture2D _read;
        int _frame;
        string _seq;
        bool _png;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("clips are taken only when " + FolderVariable + " names a folder (work order 126)");
            Directory.CreateDirectory(_folder);
            SkillLookValues.EffectsNotShapes = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _combat = CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _levels.Values = new LevelValues();
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            _brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (_brute != null) _brute.enabled = false;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "clips-log.txt"), _log);
            _log.Clear();
            SkillLookValues.EffectsNotShapes = true;
            As123(false);
            SkillLookValues.Live.KegStarburst = true;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            GameSettings.Unload();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (_combat != null) Object.Destroy(_combat.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            SmokeClouds.Clear();
            Release();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        /// <summary>123's look and its own numbers (on), or this order's (off).</summary>
        static void As123(bool on)
        {
            SkillLookValues v = SkillLookValues.Live;
            v.ToonLook = !on;
            v.SmokePuffs = on ? 22 : 30;
            v.SmokeEdgePuffs = on ? 11 : 14;
            v.KickSize = on ? 0.55f : 0.85f;
            v.MuzzleSmokeSize = on ? 0.5f : 0.6f;
            v.SlideDustSize = on ? 0.5f : 0.85f;
        }

        FeelValues Feel => _player.Feel ?? new FeelValues();
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);
        Vector3 Ahead(float metres, float across = 0f) => _stand + _ahead * metres + _side * across;

        static Vector3 MostRoom(Vector3 from, float max)
        {
            Vector3 best = Vector3.forward;
            float room = -1f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                float r = max;
                if (NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)
                    && NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas))
                    r = Vector3.Distance(Flat(hit.position), Flat(start.position));
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        // ---------------- Taking a frame ----------------

        void Prepare(int msaa)
        {
            Release();
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = Mathf.Max(1, msaa) };
            _plain = msaa > 1 ? new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) : null;
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        void Release()
        {
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
            if (_plain != null) { _plain.Release(); Object.Destroy(_plain); _plain = null; }
            if (_read != null) { Object.Destroy(_read); _read = null; }
        }

        void Render(Camera cam, string file)
        {
            RenderTexture before = cam.targetTexture;
            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = before;
            RenderTexture source = _rt;
            if (_plain != null)
            {
                Graphics.Blit(_rt, _plain); // resolves the samples
                source = _plain;
            }
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = source;
            _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            _read.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(file, _png ? _read.EncodeToPNG() : _read.EncodeToJPG(92));
        }

        /// <summary>One frame of game time, then the frame from both cameras.</summary>
        IEnumerator Shot()
        {
            yield return null;
            try
            {
                Camera cam = Camera.main;
                Vector3 keepPos = cam.transform.position;
                Quaternion keepRot = cam.transform.rotation;
                float keepFov = cam.fieldOfView;
                Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
                cam.fieldOfView = Feel.FieldOfView;
                float d = GameSettings.CameraDistance(Feel.CameraDistance);
                Vector3 her = _player.Position;
                cam.transform.SetPositionAndRotation(new Vector3(her.x, 0f, her.z) - rotation * Vector3.forward * d, rotation);
                string ext = _png ? ".png" : ".jpg";
                Render(cam, Path.Combine(_folder, _seq + "-game", _frame.ToString("0000", Inv) + ext));
                cam.transform.SetPositionAndRotation(new Vector3(_focus.x, 0.6f, _focus.z) - rotation * Vector3.forward * CloseDistance, rotation);
                Render(cam, Path.Combine(_folder, _seq + "-close", _frame.ToString("0000", Inv) + ext));
                cam.transform.SetPositionAndRotation(keepPos, keepRot);
                cam.fieldOfView = keepFov;
            }
            catch (Exception e) { _log.Add(_seq + " frame " + _frame + " FAILED: " + e.Message); }
            _frame++;
        }

        void Begin(string seq, bool png)
        {
            _seq = seq;
            _png = png;
            _frame = 0;
            Directory.CreateDirectory(Path.Combine(_folder, seq + "-game"));
            Directory.CreateDirectory(Path.Combine(_folder, seq + "-close"));
        }

        void Lay(Vector3 a, Vector3 b)
        {
            int k = 0;
            foreach (Damageable d in _dummies.Values) Put(d.transform, _stand - _ahead * 25f + _side * (k++ * 7f - 17f));
            Put(_dummies["Dummy_A"].transform, a);
            Put(_dummies["Dummy_B"].transform, b);
            Physics.SyncTransforms();
        }

        IEnumerator Fresh()
        {
            SkillEffects.Current?.ClearAll();
            SmokeClouds.Clear();
            _player.Teleport(_stand);
            _player.StartFresh();
            _player.AimAt(Ahead(6f));
            yield return null;
            yield return null;
        }

        // ---------------- The sequences ----------------

        /// <summary>The keg from the press to two seconds after its burst.</summary>
        IEnumerator Keg(string seq, bool png)
        {
            yield return Fresh();
            Lay(Ahead(6.3f, 1.8f), Ahead(6.9f, -1.7f));
            Vector3 aim = Ahead(6f);
            _focus = Vector3.Lerp(_stand, Flat(aim), 0.75f);
            int slot = Slot("Powder Keg");
            _player.EquipSkill(slot);
            _player.StartFresh();
            Begin(seq, png);
            for (int i = 0; i < 3; i++) yield return Shot();
            _player.PressSkill(slot, aim);
            _player.ReleaseSkill(slot);
            int burst = _player.KegsBurst;
            float guard = Time.time + 6f;
            while (_player.KegsBurst == burst && Time.time < guard) yield return Shot();
            float burstAt = Time.time;
            int burstFrame = _frame;
            float end = Time.time + 2f;
            while (Time.time < end) yield return Shot();
            SkillEffects fx = SkillEffects.Current;
            _log.Add(string.Format(Inv, "{0}: {1} frames, the burst at frame {2:0} ({3:0.000} s); pieces {4} pooled, {5} made, {6} capped; art made in {7:0} ms",
                                   seq, _frame, burstFrame, burstAt, fx != null ? fx.Pooled : 0, fx != null ? fx.Made : 0, fx != null ? fx.Capped : 0,
                                   SkillEffectArt.BuildMilliseconds));
        }

        /// <summary>The smoke round from the press to half a second after its cloud is gone; <paramref name="inIt"/>: thrown round her, with the brute walking in it.</summary>
        IEnumerator Smoke(string seq, bool png, bool inIt)
        {
            yield return Fresh();
            Vector3 aim;
            if (inIt)
            {
                Lay(Ahead(2.6f, 2.2f), Ahead(-0.8f, -2.4f));
                aim = Ahead(1.2f);
                _focus = Vector3.Lerp(_stand, Flat(aim), 0.5f);
                if (_brute != null)
                {
                    Vector3 at = Ahead(3.4f, -0.8f);
                    NavMeshAgent agent = _brute.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled) agent.Warp(at);
                    else Put(_brute.transform, at);
                    _brute.enabled = true;
                }
            }
            else
            {
                Lay(Ahead(6.3f, 1.8f), Ahead(6.9f, -1.7f));
                aim = Ahead(6f);
                _focus = Vector3.Lerp(_stand, Flat(aim), 0.75f);
            }
            int slot = Slot("Smoke Round");
            _player.EquipSkill(slot);
            _player.StartFresh();
            Begin(seq, png);
            for (int i = 0; i < 3; i++) yield return Shot();
            _player.PressSkill(slot, aim);
            _player.ReleaseSkill(slot);
            float guard = Time.time + 3f;
            while (SmokeClouds.Up == 0 && Time.time < guard) yield return Shot();
            float landed = Time.time;
            guard = Time.time + 6f;
            while (SmokeClouds.Up > 0 && Time.time < guard) yield return Shot();
            float gone = Time.time;
            float end = Time.time + 0.5f;
            while (Time.time < end) yield return Shot();
            if (_brute != null) _brute.enabled = false;
            SkillEffects fx = SkillEffects.Current;
            _log.Add(string.Format(Inv, "{0}: {1} frames, landed at {2:0.000} s, gone at {3:0.000} s ({4:0.000} s up); figures cleared at the end {5}",
                                   seq, _frame, landed, gone, gone - landed, fx != null ? fx.FiguresCleared : -1));
        }

        /// <summary>
        /// One of the other effects in the look (Scatter Blast's dust where its tracers end and its gun smoke, Fan Fire's held
        /// wisps, Slide's dust): from the press to 1.4 s after, the close camera on her and the cone.
        /// </summary>
        IEnumerator Other(string seq, bool png, string skill)
        {
            yield return Fresh();
            Lay(Ahead(3f, 0.6f), Ahead(4f, -1.2f));
            Vector3 aim = Ahead(6f);
            int slot = Slot(skill);
            SkillDefinition s = _player.Skills[slot];
            _focus = s.Input == SkillInput.KeyMove ? _stand + _side * 2.5f : Vector3.Lerp(_stand, Flat(aim), 0.4f);
            _player.EquipSkill(slot);
            _player.StartFresh();
            Begin(seq, png);
            for (int i = 0; i < 3; i++) yield return Shot();
            if (s.Input == SkillInput.KeyMove) _player.Steer(_side);
            _player.PressSkill(slot, aim);
            if (s.Input != SkillInput.Holding) _player.ReleaseSkill(slot);
            float start = Time.time, end = start + 1.4f;
            bool held = s.Input == SkillInput.Holding, moving = s.Input == SkillInput.KeyMove;
            while (Time.time < end)
            {
                if (held && Time.time >= start + 0.8f) { _player.ReleaseSkill(slot); held = false; }
                if (moving && Time.time >= start + 0.6f) { _player.Stop(); moving = false; }
                yield return Shot();
            }
            _log.Add(string.Format(Inv, "{0}: {1} frames", seq, _frame));
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator KegAndSmokeRound_WholeLives_At30AFrame_1920x1080_123sAndThisOrders()
        {
            Time.captureDeltaTime = Step;
            _stand = _player.Position;
            _ahead = MostRoom(_stand, 20f);
            _side = Vector3.Cross(Vector3.up, _ahead);
            foreach (string n in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(n);
                if (d == null) continue;
                Damageable body = d.GetComponent<Damageable>();
                body.SetMaxHealth(10000000);
                _dummies[n] = body;
            }
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); work order 126; " +
                     $"the game's camera {GameSettings.CameraDistance(Feel.CameraDistance):0.#} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}; the close camera {CloseDistance:0} m; " +
                     $"game time {Step:0.0000} s a frame; the drawn look's shader {(SkillEffectArt.Ready ? "found" : "MISSING")}");
            Prepare(1);
            // ROGUELIKE_CAPTURE_126_SKIP123 set: 123's look is not taken again (its frames from an earlier run stand: nothing of it changes).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_126_SKIP123")))
            {
                As123(true);
                yield return Keg("keg-123", png: false);
                yield return Smoke("smoke-123", png: false, inIt: false);
                yield return Smoke("smokein-123", png: false, inIt: true);
                yield return Other("scatter-123", png: false, "Scatter Blast");
                yield return Other("fanfire-123", png: false, "Fan Fire");
                yield return Other("slide-123", png: false, "Slide");
            }
            As123(false);
            yield return Keg("keg-126", png: true);
            yield return Smoke("smoke-126", png: true, inIt: false);
            yield return Smoke("smokein-126", png: true, inIt: true);
            yield return Other("scatter-126", png: true, "Scatter Blast");
            yield return Other("fanfire-126", png: true, "Fan Fire");
            yield return Other("slide-126", png: true, "Slide");
            SkillLookValues.Live.KegStarburst = false;
            yield return Keg("kegnostar-126", png: false);
            SkillLookValues.Live.KegStarburst = true;
            // MSAA 4x forced on the capture: the camera's target has four samples, and the pipeline asks for them as well.
            Camera cam = Camera.main;
            bool keepAllow = cam.allowMSAA;
            object asset = GraphicsSettings.currentRenderPipeline;
            PropertyInfo samples = asset != null ? asset.GetType().GetProperty("msaaSampleCount") : null;
            object keepSamples = samples != null && samples.CanRead ? samples.GetValue(asset) : null;
            try
            {
                cam.allowMSAA = true;
                if (samples != null && samples.CanWrite) samples.SetValue(asset, 4);
                Prepare(4);
                _log.Add("MSAA: the capture target has " + _rt.antiAliasing + " samples; the pipeline asset's msaaSampleCount was " + (keepSamples ?? "unknown") + ", set to 4 for the clip");
                yield return Keg("kegmsaa-126", png: true);
            }
            finally
            {
                cam.allowMSAA = keepAllow;
                if (samples != null && samples.CanWrite && keepSamples != null) samples.SetValue(asset, keepSamples);
                Prepare(1);
            }
            Time.captureDeltaTime = 0f;
        }
    }
}
