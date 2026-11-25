using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The frame rate as a small number in the top left corner (work order 128's "Show frame rate": off by default): the frames
    /// counted over <see cref="GraphicsSettingsValues.FrameRateWindowSeconds"/> of real time, shown twice a second. It exists only
    /// while the line is on (<see cref="GraphicsChoices"/> makes and removes it). Every number's words and content are made when it
    /// is made, so a frame allocates nothing.
    /// </summary>
    public sealed class FrameRateCounter : MonoBehaviour
    {
        /// <summary>The one shown now, or null.</summary>
        public static FrameRateCounter Current { get; private set; }

        /// <summary>The number shown now (frames a second, whole).</summary>
        public int Shown { get; private set; }

        /// <summary>Its place and size in the 1080-high layout, and its draw order (over everything).</summary>
        const float Left = 14f, Top = 8f, Width = 90f, Height = 30f;
        const int TextSize = 22;
        const int Depth = InterfaceDepth.Screens - 5;

        static GUIContent[] s_numbers;
        float _windowStart = -1f;
        int _frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>Makes the number if it is not shown.</summary>
        public static FrameRateCounter Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("FrameRateCounter");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<FrameRateCounter>();
            return Current;
        }

        /// <summary>Removes the number if it is shown.</summary>
        public static void Remove()
        {
            if (Current == null) return;
            FrameRateCounter c = Current;
            Current = null;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }

        void Awake()
        {
            if (s_numbers != null) return;
            s_numbers = new GUIContent[GraphicsSettingsValues.FrameRateMost + 1];
            for (int i = 0; i < s_numbers.Length; i++) s_numbers[i] = new GUIContent(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update() => Count(Time.unscaledTime);

        /// <summary>Counting starts again at the next frame (for tests).</summary>
        public void Restart()
        {
            _windowStart = -1f;
            _frames = 0;
        }

        /// <summary>A frame counted at this time (public for tests).</summary>
        public void Count(float now)
        {
            if (_windowStart < 0f)
            {
                _windowStart = now;
                _frames = 0;
                return;
            }
            _frames++;
            float span = now - _windowStart;
            if (span < GraphicsSettingsValues.FrameRateWindowSeconds) return;
            Shown = Mathf.Clamp(Mathf.RoundToInt(_frames / span), 0, GraphicsSettingsValues.FrameRateMost);
            _frames = 0;
            _windowStart = now;
        }

        void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            GUI.depth = Depth;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrix);
            InterfaceValues v = InterfaceStyle.V;
            InterfaceStyle.Label(new Rect(Left, Top, Width, Height), s_numbers[Mathf.Clamp(Shown, 0, s_numbers.Length - 1)],
                                 InterfaceStyle.Text(true, TextSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White, 0.85f), true);
            GUI.matrix = matrix;
            GUI.color = before;
        }
    }
}
