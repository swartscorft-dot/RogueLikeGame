using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The screen's edge in teal while spent focus's gains run (work order 071; the interface is work order 068's, so this
    /// is a component of its own, beside <see cref="FocusFeel"/> on its object): a soft frame from each side of the view
    /// inward, fading to nothing <see cref="FocusFeelValues.EdgeWidth"/> of the view's height in, at the feel's opacity
    /// (<see cref="FocusFeel.EdgeAlpha"/>: <see cref="FocusFeelValues.EdgeStrength"/>, flickering in the last second). It is
    /// drawn UNDER every interface element: <see cref="Depth"/> is greater than any other's (the enemy bars' 10, the cast
    /// bar's and the signs' 1, the HUD's 0 and -10). Nothing shows while a screen holds the game (the feel's edge is 0 then).
    /// Its two small gradients are made in code once; nothing is allocated in a frame.
    /// </summary>
    public sealed class FocusEdge : MonoBehaviour
    {
        /// <summary>Its GUI depth: under everything else drawn with OnGUI.</summary>
        public const int Depth = 20;
        const int Steps = 32; // texels across a gradient

        FocusFeel _feel;
        Texture2D _across, _down; // alpha from 1 at the first texel to 0 at the last: along x, along y

        /// <summary>The opacity it draws at now (0: it draws nothing).</summary>
        public float Alpha => _feel != null && _feel.isActiveAndEnabled ? _feel.EdgeAlpha : 0f;
        /// <summary>Frames it drew something in. For tests.</summary>
        public int Drawn { get; private set; }

        void Awake() => _feel = GetComponent<FocusFeel>();

        void OnDestroy()
        {
            if (_across != null) Destroy(_across);
            if (_down != null) Destroy(_down);
        }

        /// <summary>The frame's depth in pixels for a view <paramref name="height"/> high.</summary>
        public static float EdgeDepth(float height, FocusFeelValues v) => FocusFeelRules.EdgeDepth(height, v);

        /// <summary>
        /// Where its four strips lie in a view of this size: 0 the top, 1 the bottom, 2 the left, 3 the right, each the
        /// frame's depth deep, the side strips the whole height (their corners overlap: they add there, as light would).
        /// </summary>
        public static Rect Strip(int side, float width, float height, FocusFeelValues v)
        {
            float d = EdgeDepth(height, v);
            switch (side)
            {
                case 0: return new Rect(0f, 0f, width, d);
                case 1: return new Rect(0f, height - d, width, d);
                case 2: return new Rect(0f, 0f, d, height);
                default: return new Rect(width - d, 0f, d, height);
            }
        }

        void MakeTextures()
        {
            _across = Gradient(Steps, 1, true);
            _down = Gradient(1, Steps, false);
        }

        static Texture2D Gradient(int w, int h, bool alongX)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            int n = alongX ? w : h;
            for (int i = 0; i < n; i++)
            {
                float k = 1f - i / (float)(n - 1);
                var c = new Color(1f, 1f, 1f, k * k); // eased: strongest at the edge, soft inward
                if (alongX) t.SetPixel(i, 0, c);
                else t.SetPixel(0, h - 1 - i, c); // texel row 0 is the bottom: the top of the strip is the edge
            }
            t.Apply(false, false);
            return t;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float alpha = Alpha;
            if (alpha <= 1e-3f || _feel == null) return;
            if (_across == null || _down == null) MakeTextures();
            GUI.depth = Depth; // under every interface element
            FocusFeelValues v = _feel.Values;
            Color before = GUI.color;
            GUI.color = new Color(v.EdgeR, v.EdgeG, v.EdgeB, alpha);
            float w = Screen.width, h = Screen.height;
            GUI.DrawTexture(Strip(0, w, h, v), _down);                                            // top: the edge at the top
            GUI.DrawTextureWithTexCoords(Strip(1, w, h, v), _down, new Rect(0f, 1f, 1f, -1f));    // bottom: turned over
            GUI.DrawTexture(Strip(2, w, h, v), _across);                                          // left: the edge at the left
            GUI.DrawTextureWithTexCoords(Strip(3, w, h, v), _across, new Rect(1f, 0f, -1f, 1f));  // right: turned round
            GUI.color = before;
            Drawn++;
        }
    }
}
