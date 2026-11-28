using System;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 154: a busy machine made on purpose, for a builder who wants to know whether tests hold under load without
    /// waiting for a neighbour's run. With the environment variable <see cref="Variable"/> set to a number of milliseconds when
    /// Unity starts (PowerShell: <c>$env:ROGUELIKE_TEST_FRAME_MS = '120'</c> before run-tests.ps1, which hands its environment on),
    /// every frame of the PlayMode run waits that long before its Update is done: frames as long as beside a full EditMode run
    /// (there one frame carried up to a third of a second of game time). Unset (as always in the full suites), nothing is made.
    /// Its own file, after every other Update. See also <see cref="AClockAgedOnPurposeTests"/>, the other half of a long run.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class LoadOnPurpose : MonoBehaviour
    {
        /// <summary>The environment variable: milliseconds each frame is held.</summary>
        public const string Variable = "ROGUELIKE_TEST_FRAME_MS";

        /// <summary>Milliseconds each frame is held.</summary>
        public double Milliseconds;

        static readonly Stopwatch s_Watch = new Stopwatch();

        void Update()
        {
            if (AClockAgedOnPurposeTests.Ageing) return; // the clock made old first, at full speed
            s_Watch.Restart();
            while (s_Watch.Elapsed.TotalMilliseconds < Milliseconds) { }
        }

        /// <summary>The milliseconds asked for, or 0 when the variable is unset or not a number above 0.</summary>
        public static double Asked()
        {
            string value = Environment.GetEnvironmentVariable(Variable);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) && ms > 0.0 ? ms : 0.0;
        }
    }

    /// <summary>Work order 154: makes the <see cref="LoadOnPurpose"/> of the run when it is asked for, once around the whole run.</summary>
    [SetUpFixture]
    public class LoadOnPurposeSetUp
    {
        GameObject _made;

        [OneTimeSetUp]
        public void MakeTheFramesLong()
        {
            double ms = LoadOnPurpose.Asked();
            if (ms <= 0.0) return;
            _made = new GameObject("LoadOnPurpose") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(_made); // through every scene the tests load
            _made.AddComponent<LoadOnPurpose>().Milliseconds = ms;
            Debug.Log("[154] LoadOnPurpose: every frame of this run held " + ms.ToString("0.#", CultureInfo.InvariantCulture) + " ms (" + LoadOnPurpose.Variable + ")");
        }

        [OneTimeTearDown]
        public void Stop()
        {
            if (_made != null) Object.Destroy(_made);
            _made = null;
        }
    }
}
