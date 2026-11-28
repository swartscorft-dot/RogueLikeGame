using System;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 154: the game's clock made old on purpose, the other half of a long run. Two of the tests that failed only in full
    /// PlayMode runs (FocusFeltTests' faster cooldown, CleanPressJudgedTests' edges) failed because the game's clock, Time.time, a
    /// float, is held to the nearest 0.12 ms after 17 minutes, 0.24 ms after 34 and 0.49 ms after 68, and a full run's later classes
    /// start late on it; a filtered run's start young. With the environment variable <see cref="Variable"/> set to a number of
    /// seconds when Unity starts (PowerShell: <c>$env:ROGUELIKE_TEST_CLOCK_SECONDS = '5000'</c> before run-tests.ps1) and this class
    /// in the filter (<c>-Filter "AClockAgedOnPurposeTests|TheClassToCheck"</c>; its name sorts it first), its one test steps the
    /// game on at a quarter of a second of game time a frame until the clock reads that many seconds, before any other class runs.
    /// Unset (as always in the full suites), it does nothing and passes at once. See also <see cref="LoadOnPurpose"/>.
    /// </summary>
    public class AClockAgedOnPurposeTests
    {
        /// <summary>The environment variable: the game's clock, in seconds, the run's tests are to start at.</summary>
        public const string Variable = "ROGUELIKE_TEST_CLOCK_SECONDS";
        /// <summary>s of game time a frame while it ages: under Time.maximumDeltaTime's third of a second.</summary>
        const float AgeStep = 0.25f;
        /// <summary>Real seconds the ageing may take at most.</summary>
        const float RealLimit = 1200f;

        /// <summary>True while it ages the clock: <see cref="LoadOnPurpose"/> holds no frame then.</summary>
        public static bool Ageing { get; private set; }

        [UnityTest, Timeout(1500000)]
        public IEnumerator TheClock_IsMadeOld_WhenAsked()
        {
            string asked = Environment.GetEnvironmentVariable(Variable);
            if (!float.TryParse(asked, NumberStyles.Float, CultureInfo.InvariantCulture, out float target) || target <= Time.time)
            {
                TestContext.WriteLine("[154] the clock not aged (" + Variable + " unset, or the clock older): " + Time.time.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                yield break;
            }
            float from = Time.time, real = Time.realtimeSinceStartup;
            Time.timeScale = 1f;
            Time.captureDeltaTime = AgeStep;
            Ageing = true;
            try
            {
                while (Time.time < target && Time.realtimeSinceStartup - real < RealLimit) yield return null;
            }
            finally
            {
                Ageing = false;
                Time.captureDeltaTime = 0f;
            }
            string line = string.Format(CultureInfo.InvariantCulture, "[154] the clock aged on purpose from {0:0.0} to {1:0.0} s of game time in {2:0.0} s", from, Time.time, Time.realtimeSinceStartup - real);
            TestContext.WriteLine(line);
            Debug.Log(line);
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(target), line);
        }
    }
}
