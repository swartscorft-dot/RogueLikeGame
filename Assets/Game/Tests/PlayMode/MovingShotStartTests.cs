using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Where the ease of a shot on the move starts: from the speed she actually has. A standing shot that a move key
    /// turns into a shot on the move eases up from a stand; a click while she runs eases down from the run. Every frame
    /// is checked against <see cref="MovingShot.SpeedShare"/>, with the values from the settings asset.
    /// Runs go along Z = 0 toward +X from (-6, 0, 0), on open floor, with the aim straight ahead so no body turn and
    /// no backpedal cap take part.
    /// </summary>
    public class MovingShotStartTests
    {
        static readonly Vector3 RunFrom = new Vector3(-6f, 0f, 0f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        FeelValues _feel;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _feel = _player.Feel;
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        struct Frame
        {
            public float Since, Limit, Speed, Curve;
        }

        [UnityTest]
        public IEnumerator AMoveKeyInAStandingShotsWindUp_SheEasesUpFromAStand_NeverFasterThanTheCurve()
        {
            yield return StandFacingRight();
            _player.Shoot(_player.Position + Vector3.right * 8f); // open floor straight ahead
            Assume.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "a shot started");
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "in the wind-up");
            Assume.That(_player.IsShootingOnTheMove, Is.False, "a standing shot");
            Assume.That(_player.ShotTurnsBody, Is.False, "straight ahead: no body turn, no backpedal cap");
            yield return null; // one standing frame of the wind-up
            Assume.That(_player.Speed, Is.LessThan(0.01f), "she stands");
            int shotsBefore = _player.BasicShotsFired;

            _player.Steer(Vector3.right); // the key goes down: the next frame turns the shot into a shot on the move
            yield return null;
            Assert.That(_player.IsShootingOnTheMove, Is.True, "it became a shot on the move");
            float startedAt = Time.time; // the frame it became one: the ease starts here, from a stand

            // The frame the key went down has already run: measured against the curve from a stand.
            var frames = new List<Frame> { Measure(0f, startedAt, -1f) };
            Assume.That(_player.BasicShotsFired, Is.EqualTo(shotsBefore), "the shot has not gone off yet");
            bool fired = false;
            yield return RecordUntilOver(frames, 0f, startedAt, shotsBefore, f => fired = f);
            string table = Table(frames);
            TestContext.WriteLine("Standing shot turned into a shot on the move, frame by frame (m/s):\n" + table);
            _player.Stop();

            Assert.That(fired, Is.True, "the shot went off\n" + table);
            Assert.That(frames[0].Speed, Is.LessThan(0.05f), "in the frame the key goes down she is still at a stand, as the curve gives\n" + table);
            CheckEveryFrame(frames, table);
            for (int i = 1; i < frames.Count; i++)
                Assert.That(frames[i].Limit, Is.GreaterThanOrEqualTo(frames[i - 1].Limit - 1e-3f),
                            $"from a stand the limit only rises (frame {i})\n" + table);
            Frame atSlow = frames.Find(f => f.Since >= _feel.MovingShotSlowdown);
            Assert.That(atSlow.Speed, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(0.4f),
                        "at the slowed speed when the slow-down is done\n" + table);
            Assert.That(frames[frames.Count - 1].Speed, Is.EqualTo(_feel.RunSpeed).Within(0.05f), "and then at run speed\n" + table);
        }

        [UnityTest]
        public IEnumerator AClickWhileSheRuns_EasesDownFromTheRun_AndTheLimitFollowsTheCurveFromTheClick()
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right); // held, as a key
            yield return Until(() => _player.Position.x >= -3.5f, 4f);
            Assume.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.05f), "at full speed");
            Assume.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed).Within(1e-3f), "with nothing slowing her");
            int shotsBefore = _player.BasicShotsFired;

            _player.Shoot(_player.Position + Vector3.right * 8f); // the click: the shot on the move starts at once
            Assume.That(_player.IsShootingOnTheMove, Is.True, "a shot on the move");
            Assume.That(_player.ShotTurnsBody, Is.False, "straight ahead");
            float startedAt = Time.time; // the click's frame: the ease starts here, from the run

            // The click's own frame has already run: the limit and the speed she had in it.
            var frames = new List<Frame> { new Frame { Since = 0f, Limit = _player.SpeedLimit, Speed = _player.Speed, Curve = 1f } };
            bool fired = false;
            yield return RecordUntilOver(frames, 1f, startedAt, shotsBefore, f => fired = f);
            string table = Table(frames);
            TestContext.WriteLine("Click while running at full speed, frame by frame (m/s):\n" + table);
            _player.Stop();

            Assert.That(fired, Is.True, "the shot went off\n" + table);
            CheckEveryFrame(frames, table);
            Frame atSlow = frames.Find(f => f.Since >= _feel.MovingShotSlowdown);
            Assert.That(atSlow.Speed, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(0.4f),
                        "at the slowed speed when the slow-down is done\n" + table);
            Assert.That(frames[frames.Count - 1].Speed, Is.EqualTo(_feel.RunSpeed).Within(0.05f), "and back at run speed\n" + table);
        }

        // ---------------- Helpers ----------------

        /// <summary>
        /// Every frame: the speed limit is what the curve gives, and she is no faster than the limit she moved under.
        ///
        /// Work order 066 (item 3, "Farts-rykket ved skudd"): her speed read in a frame (<see cref="PlayerCharacter.Speed"/>,
        /// the NavMeshAgent's velocity) is the speed of the agent's last step, the one that brought her to this frame; that
        /// step ran at the start of this frame on the velocity set in the frame before, under the limit set then. The limit
        /// read in this frame is the one set for the next step. Before, each frame's speed was held against the curve at the
        /// frame's own time, a frame later than the limit she moved under: on a falling curve that is the curve's fall over
        /// one frame (0.013 m/s at the 0.45 ms frames of a test run, inside the 0.05 allowed), and on the one long frame of a
        /// run alone, when her first shot of the process goes off and its effects are made for the first time (9.7 ms in
        /// the probe of 2026-11-21: 4.34 read against 4.06), far more. Measured with a probe of every frame: no step was ever
        /// faster than the limit set before it, and nothing at the shot sets her speed. So each frame's speed is now held
        /// against the curve of the frame before (the click's frame against its own: she ran under the full speed then).
        /// </summary>
        void CheckEveryFrame(List<Frame> frames, string table)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                float allowed = _feel.RunSpeed * frames[i].Curve;
                float movedUnder = _feel.RunSpeed * frames[i > 0 ? i - 1 : 0].Curve; // the limit of the step that brought her here
                Assert.That(frames[i].Limit, Is.EqualTo(allowed).Within(0.01f), $"frame {i}: the speed limit is what the curve gives\n" + table);
                Assert.That(frames[i].Speed, Is.LessThanOrEqualTo(movedUnder + 0.05f), $"frame {i}: she is no faster than the curve allows\n" + table);
            }
        }

        /// <summary>The frame that has just run, measured against the curve from <paramref name="fromShare"/>.</summary>
        Frame Measure(float fromShare, float startedAt, float firedAfter)
        {
            float since = Time.time - startedAt;
            return new Frame
            {
                Since = since,
                Limit = _player.SpeedLimit,
                Speed = _player.Speed,
                Curve = MovingShot.SpeedShare(fromShare, since, firedAfter, _feel.MovingShotSpeedShare,
                                              _feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(_feel)),
            };
        }

        /// <summary>
        /// Records frame after frame until the ease is over and a little after. The shot goes off in the frame in
        /// which <see cref="PlayerCharacter.BasicShotsFired"/> goes up; the curve takes that moment from there on.
        /// </summary>
        IEnumerator RecordUntilOver(List<Frame> frames, float fromShare, float startedAt, int shotsBefore, Action<bool> setFired)
        {
            float firedAfter = _player.BasicShotsFired > shotsBefore ? Time.time - startedAt : -1f;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (firedAfter < 0f && _player.BasicShotsFired > shotsBefore) firedAfter = Time.time - startedAt;
                frames.Add(Measure(fromShare, startedAt, firedAfter));
                if (firedAfter >= 0f && MovingShot.IsOver(Time.time - startedAt - 0.1f, firedAfter, _feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(_feel)))
                    break;
            }
            setFired(firedAfter >= 0f);
        }

        IEnumerator StandFacingRight()
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right);
            yield return Until(() => Vector3.Dot(Flat(_player.transform.forward), Vector3.right) > 0.9999f && _player.Position.x > -5.5f, 3f);
            _player.Stop();
            yield return null;
            yield return null;
            Assume.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.999f), "she faces +X");
            Assume.That(_player.Speed, Is.LessThan(0.01f), "and stands");
        }

        /// <summary>
        /// Frame by frame: seconds since the ease started, the speed limit, her speed, and what the curve allows, in m/s.
        /// The first frames each, then one every 25 ms and the last: frames without a window are very short.
        /// </summary>
        string Table(List<Frame> frames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("frame  t(s)    limit  speed  curve");
            float nextAt = 0f;
            for (int i = 0; i < frames.Count; i++)
            {
                Frame f = frames[i];
                if (i >= 4 && f.Since < nextAt && i != frames.Count - 1) continue;
                if (i >= 4) nextAt = f.Since + 0.025f;
                sb.AppendLine($"{i,5}  {f.Since,6:0.000}  {f.Limit,5:0.00}  {f.Speed,5:0.00}  {f.Curve * _feel.RunSpeed,5:0.00}");
            }
            return sb.ToString();
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
