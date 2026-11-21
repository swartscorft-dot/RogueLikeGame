using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The small bars over the enemies (work order 042) in the interface's kinetic skin (decision 0011; work order 045), as
    /// part of <see cref="EnemyBars"/>: the same bars in the same places and sizes, each stack drawn slanted as one shape
    /// (its dark back, an elite's gold edge, the health in the skin's red, a guard's stagger line under it flashing the daze
    /// blue), and the marks as tiny diamonds in a row at the bar's end (the marks' orange, red at the cap, the free ones
    /// faint, Tag Round's green). The same draw calls as before: a row of diamonds is one call.
    /// </summary>
    public sealed partial class EnemyBars
    {
        /// <summary>True when the bars are drawn in the kinetic skin.</summary>
        public bool Kinetic { get; private set; }

        void DrawKinetic()
        {
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            float scale = Screen.height / Mathf.Max(1f, Values.LayoutHeight);
            float eliteEdge = Values.EliteEdge * scale;
            Color back = InterfaceStyle.C(v.Back, 1.15f), health = InterfaceStyle.C(v.Red), gold = InterfaceStyle.C(v.Gold);
            Color stagger = InterfaceStyle.C(v.Stagger), dazed = InterfaceStyle.C(v.Dazed);
            Color mark = InterfaceStyle.C(v.Marks), markFull = InterfaceStyle.C(v.Red), free = InterfaceStyle.C(v.MarkFree), tag = InterfaceStyle.C(v.Tag);
            Matrix4x4 matrixBefore = GUI.matrix;
            float k = InterfaceRules.Fight(v);
            bool grows = Mathf.Abs(k - 1f) > 1e-4f;
            // The bars are small: their plate half the strip's depth, its outline as the strip's, in real pixels of this view.
            float depth = Mathf.Max(0f, v.Depth) * 0.5f * scale * Values.LayoutHeight / Mathf.Max(1f, v.LayoutHeight);
            float outline = Mathf.Max(0f, v.Outline) * scale * Values.LayoutHeight / Mathf.Max(1f, v.LayoutHeight);
            Color plate = InterfaceStyle.C(v.DepthInk);
            for (int i = 0; i < Count; i++)
            {
                EnemyBarView b = _bars[i];
                // Work order 063: grown with the master scale of a fight, about the stack's bottom middle (it stays over the enemy).
                Matrix4x4 grown = grows ? matrixBefore * Grow(new Vector2(b.Back.center.x, b.Back.yMax), k) : matrixBefore;
                // One slant for the whole stack, about its middle.
                GUI.matrix = grown * InterfaceStyle.Shear(b.Back.center.y, v.Slant);
                // Work order 063, item 4: the stack on its dark plate, offset, a hard outline round it (one draw more).
                if (depth > 0f || outline > 0f)
                {
                    GUI.color = plate;
                    GUI.DrawTexture(new Rect(b.Back.x - outline + depth, b.Back.y - outline + depth, b.Back.width + 2f * outline, b.Back.height + 2f * outline), Texture2D.whiteTexture);
                }
                if (b.Elite)
                {
                    GUI.color = gold;
                    GUI.DrawTexture(new Rect(b.Back.x - eliteEdge, b.Back.y - eliteEdge, b.Back.width + 2f * eliteEdge, b.Back.height + 2f * eliteEdge), Texture2D.whiteTexture);
                }
                GUI.color = back;
                GUI.DrawTexture(b.Back, Texture2D.whiteTexture);
                GUI.color = health;
                GUI.DrawTexture(new Rect(b.Health.x, b.Health.y, b.Health.width * b.HealthFill, b.Health.height), Texture2D.whiteTexture);
                if (b.HasStagger)
                {
                    bool flash = b.Dazed && _flashLit;
                    GUI.color = flash ? dazed : stagger;
                    float fill = b.Dazed ? 1f : b.StaggerFill;
                    if (!b.Dazed || flash) GUI.DrawTexture(new Rect(b.Stagger.x, b.Stagger.y, b.Stagger.width * fill, b.Stagger.height), Texture2D.whiteTexture);
                }
                GUI.matrix = grown;
                // The marks: tiny diamonds from where the pips start, a little bigger than the square pips were so their points read.
                float pip = b.LitRow.height * 1.3f, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
                float x0 = b.LitRow.x, y0 = b.LitRow.center.y - pip * 0.5f;
                if (b.Lit > 0) InterfaceStyle.PipRow(new Rect(x0, y0, b.Lit * step - (step - pip), pip), b.Lit, b.AtCap ? markFull : mark);
                if (b.Pips > b.Lit) InterfaceStyle.PipRow(new Rect(x0 + b.Lit * step, y0, (b.Pips - b.Lit) * step - (step - pip), pip), b.Pips - b.Lit, free);
                if (b.Tagged) InterfaceStyle.PipRow(new Rect(x0 + b.Pips * step, y0, pip, pip), 1, tag);
                GUI.matrix = matrixBefore;
            }
            GUI.color = before;
        }

        /// <summary>A scale by <paramref name="k"/> about a point (work order 063).</summary>
        static Matrix4x4 Grow(Vector2 about, float k) =>
            Matrix4x4.Translate(new Vector3(about.x, about.y, 0f)) * Matrix4x4.Scale(new Vector3(k, k, 1f)) * Matrix4x4.Translate(new Vector3(-about.x, -about.y, 0f));
    }
}
