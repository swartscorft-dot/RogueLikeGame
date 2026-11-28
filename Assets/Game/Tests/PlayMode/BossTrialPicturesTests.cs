using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 147's LOOK: pictures taken without a window at 1920 by 1080 from the game's own camera in floor 5's throne room, the
    /// interface drawn into them by hand (<see cref="ImguiByHand"/>, as work order 130's), of each attack's tell at its start, its
    /// middle and just before it strikes (and the Wave, the ring, the seeds and Ruin as they run). It changes nothing in the game. Runs
    /// ONLY when ROGUELIKE_CAPTURE_147 names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE_147 = 'C:\Users\kriss\.claude-automation\runner\captures\147'
    ///   run-tests.ps1 PlayMode -Filter "BossTrialPicturesTests"
    /// </summary>
    public class BossTrialPicturesTests : BossTrialHarness
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_147";
        const int W = 1920, H = 1080;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator Pictures()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder))
            {
                // The merge of 2026-11-27, W3: the harness's own set-up has run (the boss's entrance off, the trial fight in use for tests,
                // nobody at the keys); an ignore here skips its tear-down, which left the entrance off for every later class of the run
                // (RoomFightsPlayTests' boss room found it off, in both full PlayModes of W3). Everything is put back before the ignore.
                CleanUp();
                Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 147)");
            }
            Directory.CreateDirectory(_folder);
            CombatEffects.Ensure();
            _levels.ShowDevBox = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            yield return null;
        }

        [TearDown]
        public void Written()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            GameSettings.Unload();
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EachAttacksTell_AtItsStartMiddleAndJustBeforeItsStrike_InBothThroneRooms()
        {
            Assert.That(LevelSeed.TryParse(HisFloor, out ulong his), Is.True);
            foreach (ulong floor in new[] { his, Floors[0] })
            {
                GeneratedLevel level = null;
                EnemyBoss boss = null;
                yield return ThroneRoom(floor, (l, b) => { level = l; boss = b; });
                string room = boss.TrialTemplate;
                _health.Untouchable = true;
                Vector3 mid = Flat(boss.TrialRoomMiddle);
                // She 7 m in front of it, toward the door, where his camera has both.
                Vector3 toDoor = Flat(_player.Position - mid).normalized;
                yield return Pose(boss, mid, mid + toDoor * 7f);

                yield return Tell(room, boss, BossTrialAttack.Cleave, mid, mid + toDoor * 4f);
                yield return Run(0.15f);
                yield return Picture(room + "-1-wave-leaving", "the Wave's path drawn for 0.3 s as it leaves, its front 1.5 m out");
                yield return Run(2f);
                yield return Tell(room, boss, BossTrialAttack.Charge, mid, mid + toDoor * 10f);
                yield return Run(2.5f);
                yield return Tell(room, boss, BossTrialAttack.GraveMarks, mid, mid + toDoor * 8f, walk: Side(toDoor));
                yield return Run(1f);

                boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f));
                yield return Run(0.3f);
                yield return Picture(room + "-0-transition", "the transition to phase 2: grey, the bar's notches");
                yield return Run(boss.TrialValues.TransitionSeconds);
                yield return Tell(room, boss, BossTrialAttack.Spokes, mid, mid + toDoor * 6f);
                yield return Run(1.5f);
                yield return Tell(room, boss, BossTrialAttack.Slam, mid, mid + toDoor * 7f);
                yield return Run(0.25f);
                yield return Picture(room + "-5-ring-0.25s", "the Shockwave 0.25 s after the landing, stopped by the pillars");
                yield return Run(0.6f);
                yield return Picture(room + "-5-ring-0.85s", "the Shockwave 0.85 s after the landing");
                yield return Run(2f);
                yield return Pose(boss, mid, mid + toDoor * 8f);
                Assert.That(boss.TrialBegin(BossTrialAttack.Seeds), Is.True);
                yield return Run(boss.TrialValues.SeedThrowWindup + 0.1f);
                yield return Picture(room + "-6-seeds-start", "three seeds just landed, their circles thin");
                yield return Run(boss.TrialValues.SeedFill * 0.5f);
                yield return Picture(room + "-6-seeds-mid", "the seeds half way to bursting");
                yield return Run(boss.TrialValues.SeedFill * 0.5f - 0.25f);
                yield return Picture(room + "-6-seeds-before", "the seeds 0.15 s before they burst");
                yield return Run(1.5f);

                boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.29f));
                yield return Run(boss.TrialValues.TransitionSeconds + 0.2f);
                if (OnFloor(mid + toDoor * 7f, 2f, out Vector3 seven)) _player.Teleport(seven);
                yield return Run(12f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.Ward);
                if (OnFloor(Flat(boss.transform.position) + toDoor * 7f, 2f, out seven)) _player.Teleport(seven); // in front of where it kneels
                yield return Run(0.1f);
                yield return Picture(room + "-7-ward-start", "Judgement: it kneels at the middle, the gold circle at its widest, the ward in the bar");
                yield return Run(boss.TrialValues.WardSeconds * 0.5f);
                yield return Picture(room + "-7-ward-mid", "the ward half way, its circle closing");
                yield return Run(boss.TrialValues.WardSeconds * 0.5f - 0.15f);
                yield return Picture(room + "-7-ward-before", "the ward's last 0.15 s");
                yield return Run(0.3f);
                yield return Picture(room + "-7-ruin-start", "Ruin told: the floor outlined red to the walls, the pillars' shadows pale");
                yield return Run(boss.RuinTime * 0.5f);
                yield return Picture(room + "-7-ruin-mid", "Ruin half way");
                yield return Run(boss.RuinTime * 0.5f - 0.35f);
                yield return Picture(room + "-7-ruin-before", "Ruin 0.15 s before it strikes");
                yield return Run(1f);
                Object.Destroy(level.gameObject);
                yield return null;
                yield return null;
            }
        }

        /// <summary>The boss at a place, she at another, both standing, the camera settled on her.</summary>
        IEnumerator Pose(EnemyBoss boss, Vector3 bossAt, Vector3 herAt)
        {
            if (OnFloor(herAt, 2f, out Vector3 h)) _player.Teleport(h);
            Place(boss, bossAt);
            yield return Run(0.5f, Stand);
        }

        /// <summary>An attack's tell at its start, its middle and just before its strike.</summary>
        IEnumerator Tell(string room, EnemyBoss boss, BossTrialAttack a, Vector3 bossAt, Vector3 herAt, Vector3 walk = default)
        {
            yield return Pose(boss, bossAt, herAt);
            Assert.That(boss.TrialBegin(a), Is.True, a + " begins");
            float windup = boss.TrialDef(a).Windup;
            string n = room + "-" + Number(a) + "-" + a.ToString().ToLowerInvariant();
            Action steer = () => { if (walk.sqrMagnitude > 0.1f) Walk(walk); else Stand(); };
            yield return Run(0.05f, steer);
            yield return Picture(n + "-start", a + " 0.05 s into its " + windup.ToString("0.00") + " s");
            yield return Run(windup * 0.5f - 0.05f, steer);
            yield return Picture(n + "-mid", a + " half way");
            yield return Run(windup * 0.5f - 0.12f, steer);
            yield return Picture(n + "-before", a + " 0.07 s before its strike");
        }

        static int Number(BossTrialAttack a) => (int)a;

        IEnumerator Picture(string name, string what)
        {
            Time.captureDeltaTime = 0f;
            yield return null;
            Camera cam = Camera.main;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = rt;
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(rt);
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                _log.Add(name + ".png: " + what);
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                rt.Release();
                Object.Destroy(rt);
            }
            Time.captureDeltaTime = Step;
        }
    }
}
