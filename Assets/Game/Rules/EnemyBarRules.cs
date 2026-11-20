using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the small bars over the enemies (work order 042; Kristian's words of 2026-11-20: "We also need small
    /// health bars over the enemies. And these stacks can be beside them. What is there now is far too big."). Sizes in
    /// pixels of a view 1080 high, scaled to the real one. First guesses.
    /// </summary>
    [Serializable]
    public sealed class EnemyBarValues
    {
        /// <summary>The switch: on, a small bar over every ordinary enemy and elite, its marks as pips beside it, its stagger under it. Off, as before.</summary>
        public bool On = true;
        /// <summary>
        /// The large marks over an enemy (MarkPips' look until work order 042) beside the small bars: off by
        /// default, the pips beside the bar taking their place. A target with no small bar (the boss, a dummy) keeps them.
        /// </summary>
        public bool BigMarks = false;
        /// <summary>The bar's width for most kinds, the swarmer's and the brute's and the guard's, and how much wider an elite's is.</summary>
        public float Width = 68f;
        public float SwarmerWidth = 60f;
        public float BigWidth = 78f;
        public float EliteExtra = 14f;
        /// <summary>The health bar's thickness, and the stagger bar's under it.</summary>
        public float Thickness = 5f;
        public float StaggerThickness = 3f;
        /// <summary>The dark edge round the bars, and the elite's gold edge outside it.</summary>
        public float Edge = 1f;
        public float EliteEdge = 1f;
        /// <summary>A mark's pip, the gap between two, and the gap from the bar's end to the first.</summary>
        public float Pip = 5f;
        public float PipGap = 2f;
        public float PipsFromBar = 3f;
        /// <summary>m over the top of the enemy's head that the bar's bottom is drawn at.</summary>
        public float OverHead = 0.35f;
        /// <summary>The height a view is laid out for.</summary>
        public float LayoutHeight = 1080f;
    }

    /// <summary>The rules of the small bars over the enemies (work order 042). No engine.</summary>
    public static class EnemyBarRules
    {
        /// <summary>
        /// True when an enemy's bar shows: alive, and hurt, marked, tagged, or with stagger taken off its bar. Hidden at full
        /// health with no marks, so a room she has not touched carries none.
        /// </summary>
        public static bool Shows(bool alive, int health, int maxHealth, int marks, bool tagged, bool staggerTaken) =>
            alive && (health < maxHealth || marks > 0 || tagged || staggerTaken);

        /// <summary>The bar's width at 1080: by kind, an elite's a little wider.</summary>
        public static float Width(EnemyKind kind, bool elite, EnemyBarValues v)
        {
            float w = kind == EnemyKind.Swarmer ? v.SwarmerWidth : kind == EnemyKind.Brute || kind == EnemyKind.Guard ? v.BigWidth : v.Width;
            return elite ? w + v.EliteExtra : w;
        }

        /// <summary>How many pips show: one per mark, and an empty one for each mark still free up to the cap.</summary>
        public static int Pips(int marks, int cap) => Math.Max(0, Math.Max(marks, cap));

        /// <summary>How many of them are lit: the marks it carries.</summary>
        public static int LitPips(int marks, int cap) => Math.Max(0, Math.Min(marks, Pips(marks, cap)));

        /// <summary>The width of a row of <paramref name="count"/> pips at 1080, no gap after the last.</summary>
        public static float PipsWidth(int count, EnemyBarValues v) => count <= 0 ? 0f : count * (v.Pip + v.PipGap) - v.PipGap;

        /// <summary>The height of the stack of bars at 1080: the health bar, and the stagger bar under it when it has one.</summary>
        public static float StackHeight(bool stagger, EnemyBarValues v) => v.Thickness + (stagger ? v.Edge + v.StaggerThickness : 0f);

        /// <summary>
        /// The draw calls one bar costs: its dark back, the gold edge of an elite, the health, the stagger, the lit pips and
        /// the empty ones (each row is one call, the pips repeated along it), the tag's pip.
        /// </summary>
        public static int DrawCalls(bool elite, bool stagger, int lit, int empty, bool tagged) =>
            1 + (elite ? 1 : 0) + 1 + (stagger ? 1 : 0) + (lit > 0 ? 1 : 0) + (empty > 0 ? 1 : 0) + (tagged ? 1 : 0);
    }
}
