namespace Roguelike.Rules
{
    /// <summary>
    /// The camera's distance as a setting he can step while playing (work order 083; Kristian, 2026-11-22, on the cave's
    /// pictures: "Er kameraet mer zoomet inn og veggene høyere?"). Step 0 is no override: the feel's own
    /// <see cref="FeelValues.CameraDistance"/> (28 m since 2026-11-19, after he found 14 too close: "much better"; the default
    /// stays). Steps 1 and 2 bring it to 25 and 22 m, and the next step goes back to 0. Kept in PlayerPrefs by the game's
    /// settings, as the camera's kick is. Plain data.
    /// </summary>
    public static class CameraDistanceSteps
    {
        /// <summary>The distances of steps 1 and 2, in metres: the order's "28, 25, 22 and back".</summary>
        public static readonly float[] Closer = { 25f, 22f };

        /// <summary>How many steps there are: the feel's own distance and the closer ones.</summary>
        public static int Count => Closer.Length + 1;

        /// <summary>A stored value made a step: anything that is not a step is step 0 (no override).</summary>
        public static int Step(int stored) => stored >= 0 && stored < Count ? stored : 0;

        /// <summary>The step after this one: 0, 1, 2 and back to 0.</summary>
        public static int Next(int step) => (Step(step) + 1) % Count;

        /// <summary>The step before this one (the setting's left arrow): 0, 2, 1 and back to 0.</summary>
        public static int Previous(int step) => (Step(step) + Count - 1) % Count;

        /// <summary>The distance in use for a step: the feel's own at step 0, else the closer step's.</summary>
        public static float Distance(int step, float feelDistance)
        {
            int s = Step(step);
            return s == 0 ? feelDistance : Closer[s - 1];
        }
    }
}
