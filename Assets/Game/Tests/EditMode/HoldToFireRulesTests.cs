using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Hold to fire (work order 132; Kristian ticked "Ja, hold = skyt"), as the rules see it: the switch on by default and off as
    /// before; the held button asks for a shot only on the frames a click would start one at once, so over ten seconds holding
    /// fires exactly the shots perfect clicking fires, at the same moments, at any frame rate, and never faster than the shot's
    /// own pace; and it asks for none wherever a click is refused or made to wait.
    ///
    /// The shot's cycle is driven here by the game's own pieces as <see cref="ClickToShoot"/> and <see cref="ActionTimeline"/>
    /// are used by the character each frame (the input first, then her Update: the shot goes off, gives way when its time is
    /// over, and a click that waited is taken), with the feel's own numbers.
    /// </summary>
    public class HoldToFireRulesTests
    {
        static readonly FeelValues Feel = new FeelValues();
        const float Seconds = 10f;

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndOffTheHeldButtonAsksForNothing()
        {
            Assert.That(HoldToFireValues.On, Is.True, "his tick: \"Ja, hold = skyt\"");
            Assert.That(HoldToFireRules.Fires(true, true, false, true, false, false), Is.True, "on: held, free, a shot");
            Assert.That(HoldToFireRules.Fires(false, true, false, true, false, false), Is.False, "off: holding never repeats, as before");
        }

        [Test]
        public void SwitchOff_OverTenSeconds_HoldingFiresOnlyThePress_AsBefore()
        {
            List<float> held = Run(1f / 60f, holder: true, on: false, busyFrom: -1f, busyTo: -1f);
            Assert.That(held.Count, Is.EqualTo(1), "one click, one shot");
            Assert.That(held[0], Is.EqualTo(0f));
        }

        // ---------------- Its pace ----------------

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        [TestCase(0.0003f)] // a frame without a window
        public void OverTenSeconds_HoldingFiresTheShotsOfPerfectClicking_AtTheSameMoments_AndNoFaster(float dt)
        {
            List<float> clicked = Run(dt, holder: false, on: true, busyFrom: -1f, busyTo: -1f);
            List<float> held = Run(dt, holder: true, on: true, busyFrom: -1f, busyTo: -1f);
            Assert.That(held.Count, Is.EqualTo(clicked.Count), "the same shots in ten seconds");
            for (int i = 0; i < held.Count; i++) Assert.That(held[i], Is.EqualTo(clicked[i]), "shot " + i + " starts when a click's would");
            float interval = Feel.BasicShotStartup + Feel.BasicShotActive + Feel.BasicShotRecovery;
            for (int i = 1; i < held.Count; i++) Assert.That(held[i] - held[i - 1], Is.GreaterThanOrEqualTo(interval - 1e-5f), "never sooner than the shot's own pace");
            int most = (int)Math.Floor(Seconds / interval) + 1;
            Assert.That(held.Count, Is.LessThanOrEqualTo(most), "no faster than the shot allows");
            Assert.That(held.Count, Is.GreaterThanOrEqualTo((int)Math.Floor(Seconds / (interval + dt))), "and as fast as it allows: a frame a shot lost at most");
            TestContext.WriteLine($"dt {dt:0.0000} s: {held.Count} shots in {Seconds} s held, {clicked.Count} clicked (the shot's {interval:0.000} s allows {most})");
        }

        [Test]
        public void OverTenSeconds_AtUnevenFrames_HoldingStillFiresTheShotsOfPerfectClicking()
        {
            var rng = new Random(132);
            var frames = new List<float>();
            for (float t = 0f; t < Seconds; t += 0.004f + (float)rng.NextDouble() * 0.03f) frames.Add(t);
            List<float> clicked = Run(frames, holder: false, on: true, busyFrom: -1f, busyTo: -1f);
            List<float> held = Run(frames, holder: true, on: true, busyFrom: -1f, busyTo: -1f);
            Assert.That(held, Is.EqualTo(clicked));
        }

        [Test]
        public void AfterSomethingElseKeptHer_TheHeldButtonFiresAtMostAFrameAfterAClickThatWaited_NeverBefore()
        {
            // Something that is not a shot keeps her from 1.0 s to 1.7 s (a skill, a dash, a stun, a duck).
            const float dt = 1f / 60f;
            List<float> clicked = Run(dt, holder: false, on: true, busyFrom: 1f, busyTo: 1.7f);
            List<float> held = Run(dt, holder: true, on: true, busyFrom: 1f, busyTo: 1.7f);
            float firstClicked = clicked.Find(t => t >= 1f), firstHeld = held.Find(t => t >= 1f);
            Assert.That(firstHeld, Is.GreaterThanOrEqualTo(1.7f - 1e-5f), "nothing while she is kept");
            Assert.That(firstHeld, Is.GreaterThanOrEqualTo(firstClicked), "never before a click");
            Assert.That(firstHeld - firstClicked, Is.LessThanOrEqualTo(dt + 1e-5f), "a frame after at most");
        }

        // ---------------- Refused wherever a click is ----------------

        [Test]
        public void TheHeldButton_AsksForNothing_WhereAClickIsRefusedOrMadeToWait()
        {
            Assert.That(HoldToFireRules.Fires(true, false, false, true, false, false), Is.False, "not held");
            Assert.That(HoldToFireRules.Fires(true, true, true, true, false, false), Is.False, "after a choice or a screen, until the button has gone up");
            Assert.That(HoldToFireRules.Fires(true, true, false, false, false, false), Is.False, "a click now would wait: nothing is asked");
            Assert.That(HoldToFireRules.Fires(true, true, false, true, true, false), Is.False, "sprinting or in a jump: a click is refused");
            Assert.That(HoldToFireRules.Fires(true, true, false, true, false, true), Is.False, "a skill on the same button holds or waits for its recast");

            // When a click would go at once: never while something else keeps her, nor in the last shot's time, nor before it went off.
            Assert.That(HoldToFireRules.ClickGoesThisFrame(false, false, false, 5f, 4f), Is.True, "free");
            Assert.That(HoldToFireRules.ClickGoesThisFrame(true, false, false, 5f, 4f), Is.False, "a skill, a dash, a stun or a pull, a jump, a duck");
            Assert.That(HoldToFireRules.ClickGoesThisFrame(false, false, false, 3.9f, 4f), Is.False, "the last shot's time is not over");
            Assert.That(HoldToFireRules.ClickGoesThisFrame(false, true, false, 5f, 4f), Is.False, "the shot going on has not gone off");
            Assert.That(HoldToFireRules.ClickGoesThisFrame(false, true, true, 5f, 4f), Is.True, "it went off and its time is over: it gives way this frame");
            Assert.That(HoldToFireRules.ClickGoesThisFrame(false, true, true, 4f, 4f), Is.True, "the edge counts, as ClickToShoot's wait");
            Assert.That(ClickToShoot.MustWait(false, 4f, 4f), Is.False, "the same edge as a click's");
        }

        // ---------------- The shot's cycle, as the character runs it ----------------

        static List<float> Run(float dt, bool holder, bool on, float busyFrom, float busyTo)
        {
            var frames = new List<float>();
            int n = (int)Math.Ceiling(Seconds / dt);
            for (int i = 0; i < n; i++) frames.Add(i * dt);
            return Run(frames, holder, on, busyFrom, busyTo);
        }

        /// <summary>
        /// The moments shots started over the frames given: a clicker who clicks on every frame (the most a click can do: a click
        /// while she cannot shoot waits and goes the moment it may), or a holder who presses on the first frame and holds.
        /// </summary>
        static List<float> Run(List<float> frames, bool holder, bool on, float busyFrom, float busyTo)
        {
            var gun = new Gun(Feel);
            for (int i = 0; i < frames.Count; i++)
            {
                float now = frames[i];
                gun.OtherBusy = now >= busyFrom && now < busyTo;
                bool pressed = holder ? i == 0 : true;
                // The input driver: a press is a click; else the held button, by the rule.
                if (pressed || HoldToFireRules.Fires(on, true, false, gun.ClickGoesThisFrame(now), false, false)) gun.Click(now);
                gun.Update(now);
            }
            return gun.Starts;
        }

        /// <summary>The basic shot's cycle as <c>PlayerCharacter</c> runs it (Shoot, and its Update for a standing shot).</summary>
        sealed class Gun
        {
            readonly ClickToShoot _clicks = new ClickToShoot();
            readonly ActionTimeline _action = new ActionTimeline();
            readonly float _startup, _active, _recovery;
            float _nextShotAt = float.NegativeInfinity, _startedAt;
            bool _shooting;
            public bool OtherBusy;
            public readonly List<float> Starts = new List<float>();

            public Gun(FeelValues f)
            {
                _startup = f.BasicShotStartup;
                _active = f.BasicShotActive;
                _recovery = f.BasicShotRecovery;
            }

            bool Busy => _shooting || OtherBusy;

            public bool ClickGoesThisFrame(float now) => HoldToFireRules.ClickGoesThisFrame(OtherBusy, _shooting, _shooting && _action.HitResolved, now, _nextShotAt);

            public void Click(float now)
            {
                if (_clicks.Click(Busy, now, _nextShotAt)) Start(now);
            }

            public void Update(float now)
            {
                if (_shooting)
                {
                    if (_action.ConsumeHit(now)) _nextShotAt = _startedAt + _action.Total; // only a shot that went off makes the next one wait
                    if (_action.IsRunning(now)) return;
                    _action.Cancel();
                    _shooting = false;
                }
                if (OtherBusy) return;
                if (_clicks.TakeWhenAllowed(Busy, now, _nextShotAt)) Start(now); // free: a waiting click
            }

            void Start(float now)
            {
                _shooting = true;
                _startedAt = now;
                _action.Start(now, _startup, _active, _recovery);
                Starts.Add(now);
            }
        }
    }
}
