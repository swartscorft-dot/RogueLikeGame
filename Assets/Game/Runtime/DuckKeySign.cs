using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The duck's key sign (work order 132; Kristian, 2026-11-25, of the quick duck's orange rim that a friend did not understand:
    /// "Ctrl/shift når det er mulig"; the switch and the numbers are <see cref="DuckKeySignValues"/>). While the rim shows
    /// (<see cref="PlayerCharacter.DuckRimOn"/>: a tap of the cover key would duck her behind that prop), a small key plate stands
    /// just beside the rim on the screen, on the side away from her, naming the key the cover action is bound to now
    /// (<see cref="InputBindings.TakeCover"/>, read from the bindings in use: Left Ctrl reads "Ctrl"). It is the chest prompt's key
    /// in the interface's skin (<see cref="InterfaceStyle"/>: the white slanted plate with the key in black on the faint dark back),
    /// without words and a little smaller, drawn at <see cref="DuckKeySignValues.Alpha"/>: it is on screen often. It shows
    /// exactly while the rim does, and changes nothing in play.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="PlayerSigns"/> (never in a test run); tests make one
    /// with <see cref="Ensure"/>. Where it stands is worked out on the repaint, after the camera has moved for the frame; nothing
    /// is allocated while nothing changes (the key's words are made once for each key).
    /// </summary>
    // After her Update (where the rim is shown or hidden for the frame), as the skill bar and the cast bar read her: the sign
    // is up exactly in the frames the rim is, from that frame's Update on.
    [DefaultExecutionOrder(100)]
    public sealed class DuckKeySign : MonoBehaviour
    {
        /// <summary>Points round a round prop's rim that its box on the screen is taken from.</summary>
        const int CirclePoints = 8;

        /// <summary>The one running now, or null.</summary>
        public static DuckKeySign Current { get; private set; }

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>True while the sign is up: the switch on, the rim showing, and a key bound.</summary>
        public bool Showing { get; private set; }
        /// <summary>The words on the sign: the cover key as the bindings have it now ("Ctrl").</summary>
        public string KeyText => _label;
        /// <summary>Where the plate is, in layout pixels (a view 1080 high, y down), as last laid out; and the rim's box on the screen.</summary>
        public Rect PlateRect { get; private set; }
        public Rect RimRect { get; private set; }
        /// <summary>True when the last lay-out found a place for it (the rim in front of the camera).</summary>
        public bool Placed { get; private set; }

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        Key _key = Key.None;
        bool _keyRead;
        string _label = "";
        float _labelWidth = -1f;
        readonly GUIContent _content = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static DuckKeySign Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("DuckKeySign");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<DuckKeySign>();
            return Current;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update() => Tick();

        /// <summary>The frame's work: whether the sign is up, and its words. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
                _driver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
            }
            ReadKey();
            bool alive = _player != null && (_health == null || _health.IsAlive);
            Showing = DuckKeySignValues.On && alive && _player.DuckRimOn != null && _label.Length > 0;
        }

        /// <summary>The cover key's words, made again only when the binding changes.</summary>
        void ReadKey()
        {
            InputBindings b = _driver != null ? _driver.ActiveBindings : null;
            Key key = b != null ? b.TakeCover : Key.LeftCtrl;
            if (_keyRead && key == _key) return;
            _keyRead = true;
            _key = key;
            _label = DuckKeySignRules.KeyLabel(key.ToString());
            _content.text = _label;
            _labelWidth = -1f;
        }

        /// <summary>
        /// Where the plate goes now (<see cref="PlateRect"/>, <see cref="RimRect"/>): the rim's outer edge at its height, seen
        /// through the camera, as a box on the screen in layout pixels, and the plate beside it away from her
        /// (<see cref="DuckKeySignRules.PlateLeft"/>). False when it is not up or the rim is not in front of the camera.
        /// Called on the repaint, and by tests.
        /// </summary>
        public bool Layout()
        {
            Placed = false;
            if (!Showing || _player == null) return false;
            Camera cam = Camera.main;
            Shelter on = _player.DuckRimOn;
            if (cam == null || on == null || !on.TryShape(out CoverShape shape)) return false;
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale), screenHeight = InterfaceStyle.ScreenHeight;
            float height = _player.DuckRim.Height;
            float grow = QuickDuckRim.Outset + QuickDuckRim.Width;
            float left = float.PositiveInfinity, right = float.NegativeInfinity, top = float.PositiveInfinity, bottom = float.NegativeInfinity;
            var centre = new Vector3(shape.Centre.X, height, shape.Centre.Z);
            int points = shape.Kind == CoverShapeKind.Circle ? CirclePoints : 4;
            float yaw = shape.Yaw * Mathf.Deg2Rad;
            var across = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            var along = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            for (int i = 0; i < points; i++)
            {
                Vector3 p;
                if (shape.Kind == CoverShapeKind.Circle)
                {
                    float a = i * Mathf.PI * 2f / CirclePoints;
                    p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (shape.Radius + grow);
                }
                else
                {
                    float sx = i == 0 || i == 3 ? -1f : 1f, sz = i < 2 ? -1f : 1f; // the box's corners in turn, as the rim draws them
                    p = centre + across * (sx * (shape.Width * 0.5f + grow)) + along * (sz * (shape.Depth * 0.5f + grow));
                }
                Vector3 s = cam.WorldToScreenPoint(p);
                if (s.z <= 0f) return false;
                float x = s.x / scale, y = (screenHeight - s.y) / scale;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                top = Mathf.Min(top, y);
                bottom = Mathf.Max(bottom, y);
            }
            Vector3 her = cam.WorldToScreenPoint(_player.Position);
            RimRect = new Rect(left, top, right - left, bottom - top);
            GUIStyle style = InterfaceStyle.Text(true, DuckKeySignValues.KeySize, TextAnchor.MiddleCenter);
            if (_labelWidth < 0f) _labelWidth = style.CalcSize(_content).x;
            float h = DuckKeySignValues.KeySize * DuckKeySignValues.PlateHeightShare;
            float w = Mathf.Max(DuckKeySignValues.PlateLeastWidth, _labelWidth + 2f * DuckKeySignValues.PlatePad);
            float x0 = DuckKeySignRules.PlateLeft(left, right, top, her.x / scale, w, h, out float y0);
            PlateRect = new Rect(x0, y0, w, h);
            Placed = true;
            return true;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Showing || !Layout()) return;
            InterfaceValues v = InterfaceStyle.V;
            Color colorBefore = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 before);
            float a = DuckKeySignValues.Alpha, m = 3f; // the prompt's own margin round its key plate (InterfaceStyle.Prompt)
            Rect plate = PlateRect;
            InterfaceStyle.Slanted(new Rect(plate.x - m, plate.y - m, plate.width + 2f * m, plate.height + 2f * m), InterfaceStyle.C(v.Back, a));
            InterfaceStyle.Slanted(plate, InterfaceStyle.C(v.White, a));
            InterfaceStyle.Label(plate, _content, InterfaceStyle.Text(true, DuckKeySignValues.KeySize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
            GUI.color = colorBefore;
            GUI.matrix = before;
        }
    }
}
