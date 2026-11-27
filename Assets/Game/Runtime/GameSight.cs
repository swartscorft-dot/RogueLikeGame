using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The sight (work order 144; Kristian, playing main on 2026-11-26: "vi har ingen muspeker design" and "hvordan vi skulle time
    /// timing baserte skills"; his pick, round 1's sheet 2, "armen langs ringen"): the game's own pointer while she plays. The
    /// Windows arrow is hidden and the sight is drawn at the cursor: the centre dot exactly on the cursor's point, a thin teal ring
    /// with its dark outline, and the bronze arm folded at its pin; four gunmetal brackets with a dim teal line round the ring
    /// stand at the edges of work order 143's cone at the cursor's distance (<see cref="PrecisionSight.Cone"/>), snug while she is
    /// accurate (<see cref="SightValues.PointerStays"/>: on, only the brackets travel; off, the whole ring spreads, his sheet's
    /// sixth drawing). With the "Sight" timing cue (<see cref="TimingCue.Sight"/>) a timed press is shown by the arm: its head
    /// climbs from the pin at one rate and a zone of teeth stands where its catch is while the press is clean (<see cref="SightArm"/>,
    /// work order 142's rule); "now" the ring's line and the head turn white and the teeth teal; too late the ring's line goes grey
    /// and the arm drops back to its pin in a dull red.
    ///
    /// The Windows arrow comes back whenever the sight is not drawn: a screen with buttons up (the pause menu, an offer, an end
    /// screen, the screen between runs, the start menu, the settings screen, the room lab's picker), she dead, the game without
    /// focus, this piece gone (a scene left, Play ended). Nothing pulls toward an enemy: it reads her, the cursor, the camera and
    /// the judge's clock only. It installs itself when Play starts under the same rule as the other pieces (never in a test run);
    /// tests make one with <see cref="Ensure"/>. Its mesh is filled again only when the sight changes: nothing is allocated in a frame.
    /// </summary>
    [DefaultExecutionOrder(Order)]
    public sealed class GameSight : MonoBehaviour
    {
        /// <summary>Its place in the frame: after the timing cue has followed the chain and work order 143 has measured the cone.</summary>
        public const int Order = PrecisionSight.Order + 10;
        const float LayoutHeight = 1080f; // px: the sizes are given for a screen this high

        public static GameSight Current { get; private set; }

        /// <summary>The numbers (reachable in the Inspector while playing).</summary>
        public SightValues Values;
        SightValues V => Values ?? SightValues.Live;

        public bool StartedByItself { get; private set; }

        /// <summary>
        /// True while the sight draws 143's cone (its switch on and it running): <see cref="PrecisionSight"/> then draws no brackets of
        /// its own. Off, 143's brackets as 143 made them.
        /// </summary>
        public static bool TakesTheBrackets => SightValues.On && Current != null && Current.isActiveAndEnabled;

        /// <summary>True while this piece keeps the Windows arrow hidden.</summary>
        public static bool ArrowHidden => s_arrowHidden;

        /// <summary>For pictures: the cone in degrees to open the brackets to, whatever her own (null: hers, from work order 143).</summary>
        public static float? ConeForPictures;

        // ---------------- What it shows, worked out every frame ----------------

        /// <summary>The sight is drawn this frame.</summary>
        public bool Shown { get; private set; }
        /// <summary>Its middle, in the interface's screen pixels from the top left: the cursor's point.</summary>
        public Vector2 Centre { get; private set; }
        /// <summary>The opening in use: the pointer stays and the brackets travel (else the whole ring spreads).</summary>
        public bool PointerStays { get; private set; }
        /// <summary>The cone at the cursor: its degrees, its width on the screen (screen px), and how far the brackets (or segments) stand out (layout px).</summary>
        public float ConeDegrees { get; private set; }
        public float ConePixels { get; private set; }
        public float Shift { get; private set; }
        /// <summary>The "Sight" cue is in use and a press is timed: the zone of teeth is drawn.</summary>
        public bool Timing { get; private set; }
        /// <summary>The zone is drawn (while timed, or lingering after a window ran out), its two ends (start &gt; end), its opacity.</summary>
        public bool ZoneShown { get; private set; }
        public float ZoneStart { get; private set; }
        public float ZoneEnd { get; private set; }
        public float ZoneAlpha { get; private set; }
        /// <summary>The head's middle and its catch's leading edge, as drawn (degrees clockwise from the top).</summary>
        public float HeadAngle { get; private set; }
        public float LeadAngle { get; private set; }
        /// <summary>The catch touches or lies in the zone, as drawn.</summary>
        public bool CatchInZone { get; private set; }
        /// <summary>"Now": the window is open and the catch in the zone. "Too late": the clean part behind, or the window ran out unpressed.</summary>
        public bool Now { get; private set; }
        public bool Late { get; private set; }
        /// <summary>How many times its mesh was filled (when the sight changed). For tests.</summary>
        public int Builds { get; private set; }

        static bool s_arrowHidden;

        PlayerCharacter _player;
        PlayerHealth _health;
        bool _focused = true;
        bool _wasTiming;
        int _lastPress;
        float _lateUntil = float.NegativeInfinity;
        float _lingerStart, _lingerEnd;
        Mesh _mesh;
        Material _material;
        readonly SightShapes _shapes = new SightShapes();
        readonly Vector2[] _corners = new Vector2[8];
        BuiltFor _built;
        bool _hasBuilt;

        /// <summary>What the mesh was built for: when it differs, the mesh is filled again.</summary>
        struct BuiltFor
        {
            public float Shift, ZoneStart, ZoneEnd, ZoneAlpha, Head, Feather;
            public bool Stays, Zone, Now, Late;

            public bool Same(BuiltFor o) => Shift == o.Shift && ZoneStart == o.ZoneStart && ZoneEnd == o.ZoneEnd && ZoneAlpha == o.ZoneAlpha && Head == o.Head
                                       && Feather == o.Feather && Stays == o.Stays && Zone == o.Zone && Now == o.Now && Late == o.Late;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            ConeForPictures = null;
            if (s_arrowHidden) Cursor.visible = true;
            s_arrowHidden = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SightValues.On || !SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static GameSight Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("GameSight");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<GameSight>();
            return Current;
        }

        void Awake()
        {
            if (Values == null) Values = SightValues.Live;
            _focused = Application.isFocused || Application.isBatchMode;
        }

        void OnDisable()
        {
            Shown = false;
            Arrow(false);
        }

        void OnDestroy()
        {
            Arrow(false);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (Current == this) Current = null;
        }

        void OnApplicationQuit() => Arrow(false);

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) Arrow(false);
        }

        /// <summary>The game lost or gained focus (the engine's own message; public for tests).</summary>
        public void FocusChanged(bool focused) => OnApplicationFocus(focused);

        /// <summary>Hides or shows the Windows arrow, only when that changes (nothing else of the game sets it).</summary>
        static void Arrow(bool hide)
        {
            if (hide == s_arrowHidden) return;
            s_arrowHidden = hide;
            Cursor.visible = !hide;
        }

        void LateUpdate() => Tick();

        /// <summary>The frame's work: whether it is drawn, where, the opening and the arm. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            }
            PlayerCharacter p = _player;
            Shown = SightValues.On && p != null && (_health == null || _health.IsAlive) && !ScreenHolds();
            Arrow(Shown && _focused);
            if (!Shown)
            {
                Timing = ZoneShown = Now = Late = CatchInZone = false;
                _wasTiming = false;
                return;
            }
            SightValues v = V;
            Centre = CentreNow(p);
            PointerStays = SightSettings.PointerStaysNow;
            Opening(p, v);
            Arm(p, v);
        }

        /// <summary>A screen with buttons, or one that holds the game, is up: the Windows arrow is wanted.</summary>
        static bool ScreenHolds()
        {
            if (PlayerSigns.ScreenUp || StartMenu.Holds || SettingsScreen.Holds) return true; // an offer is a choice: ScreenUp holds it
            GeneratedLevels levels = GeneratedLevels.Current;
            return levels != null && levels.PickerOpen;
        }

        /// <summary>Where the cursor is: the mouse itself, or (no real mouse read, or a picture) her cursor's ray through the camera, as the timing cue's sight.</summary>
        Vector2 CentreNow(PlayerCharacter p)
        {
            float height = InterfaceStyle.ScreenHeight;
            if (PlayerInputDriver.ReadRealDevices && Mouse.current != null && InterfaceStyle.ScreenForPictures.x <= 0)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                return new Vector2(m.x, height - m.y);
            }
            Camera cam = Camera.main;
            if (cam == null) return Centre;
            Vector3 s = cam.WorldToScreenPoint(p.CursorOnItsRay);
            if (s.z <= 0f) return Centre;
            return new Vector2(s.x, height - s.y);
        }

        /// <summary>The cone at the cursor (work order 143's one entry), made the brackets' travel.</summary>
        void Opening(PlayerCharacter p, SightValues v)
        {
            float pixels = 0f, degrees = 0f;
            if (ConeForPictures.HasValue)
            {
                degrees = ConeForPictures.Value;
                pixels = PixelsAcross(Camera.main, p.Position, p.CursorOnItsRay, degrees);
            }
            else
            {
                PrecisionSight precision = PrecisionSight.Current;
                if (precision != null && precision.Cone.Shown)
                {
                    degrees = precision.Cone.Degrees;
                    pixels = precision.Cone.Pixels;
                }
            }
            ConeDegrees = degrees;
            ConePixels = pixels;
            float scale = InterfaceStyle.ScreenHeight / LayoutHeight;
            Shift = SightOpening.Shift(pixels * 0.5f / Mathf.Max(1e-3f, scale), v);
        }

        /// <summary>The cone's width at the cursor on the screen (screen px), as work order 143 measures it (<see cref="PrecisionSight.Measure"/>), for a given cone.</summary>
        public static float PixelsAcross(Camera cam, Vector3 her, Vector3 sight, float degrees)
        {
            if (cam == null || degrees <= 0f) return 0f;
            var flat = new Vector3(sight.x - her.x, 0f, sight.z - her.z);
            float d = flat.magnitude;
            Vector3 along = d > 1e-4f ? flat / d : Vector3.forward;
            var across = new Vector3(along.z, 0f, -along.x);
            float half = PrecisionRules.WidthAt(degrees, d) * 0.5f;
            Vector3 a = cam.WorldToScreenPoint(sight + across * half), b = cam.WorldToScreenPoint(sight - across * half);
            return new Vector2(a.x - b.x, a.y - b.y).magnitude;
        }

        /// <summary>The arm: with the "Sight" cue, the press waited for on the cues' one clock; else folded at its pin.</summary>
        void Arm(PlayerCharacter p, SightValues v)
        {
            TimingAtTheSight cue = TimingAtTheSight.Current;
            bool sightCue = cue != null && cue.isActiveAndEnabled && cue.Cue == TimingCue.Sight;
            Timing = sightCue && cue.ChainUp && cue.Press > 0 && cue.Clean.Any;
            float rest = SightArm.RestAngle(v);
            if (Timing)
            {
                float lead = p.RecastLead;
                SightArm.Zone(cue.Clean, lead, v, out float start, out float end);
                ZoneStart = start;
                ZoneEnd = end;
                ZoneShown = true;
                ZoneAlpha = cue.Alpha;
                Late = SightArm.IsLate(cue.SinceOpened, cue.Window, cue.Clean);
                // Too late: back at its pin, never back into the zone.
                HeadAngle = Late ? rest : SightArm.HeadAngle(SightArm.SincePress(cue.Since, lead), v);
                LeadAngle = SightArm.LeadAngle(HeadAngle, v);
                CatchInZone = SightArm.CatchInZone(LeadAngle, start, end, v);
                Now = cue.WindowOpen && CatchInZone && !Late;
                _lastPress = cue.Press;
                _wasTiming = true;
                return;
            }
            // A window run out unpressed: too late, lingering as the cue's own linger (TimingAtTheSightValues.LingerSeconds).
            if (_wasTiming && sightCue && !cue.ChainUp && p.RecastTimingOf(_lastPress) == RecastTiming.Missed)
            {
                _lateUntil = Time.time + Mathf.Max(0f, cue.Values != null ? cue.Values.LingerSeconds : TimingAtTheSightValues.Live.LingerSeconds);
                _lingerStart = ZoneStart;
                _lingerEnd = ZoneEnd;
            }
            _wasTiming = false;
            Late = sightCue && Time.time < _lateUntil;
            ZoneShown = Late;
            ZoneStart = Late ? _lingerStart : 0f;
            ZoneEnd = Late ? _lingerEnd : 0f;
            ZoneAlpha = Late ? cue.Alpha : 0f;
            Now = CatchInZone = false;
            HeadAngle = rest;
            LeadAngle = SightArm.LeadAngle(rest, v);
        }

        // ---------------- The drawing ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Shown) return;
            GUI.depth = V.Depth;
            if (!Ready()) return;
            float scale = InterfaceStyle.ScreenHeight / LayoutHeight;
            Build(scale);
            _material.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight, 0f);
            Graphics.DrawMeshNow(_mesh, Matrix4x4.TRS(new Vector3(Centre.x, Centre.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f)));
            GL.PopMatrix();
        }

        /// <summary>The mesh and its material, made once: the engine's own plain coloured shader, drawn over everything with no depth.</summary>
        bool Ready()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return false;
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _material.SetInt("_Cull", (int)CullMode.Off);
                _material.SetInt("_ZWrite", 0);
                _material.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "GameSight", hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
            }
            return true;
        }

        /// <summary>Fills the mesh again when the sight has changed since it was last filled (public for tests: the shapes and their count).</summary>
        public void Build(float scale)
        {
            SightValues v = V;
            var key = new BuiltFor
            {
                Shift = Shift, ZoneStart = ZoneStart, ZoneEnd = ZoneEnd, ZoneAlpha = ZoneAlpha, Head = HeadAngle, Feather = v.Feather / Mathf.Max(1e-3f, scale),
                Stays = PointerStays, Zone = ZoneShown, Now = Now, Late = Late,
            };
            if (_hasBuilt && key.Same(_built)) return;
            if (_mesh == null && !Ready()) return;
            _built = key;
            _hasBuilt = true;
            Builds++;
            SightShapes s = _shapes;
            s.Clear();
            s.Feather = key.Feather;
            s.Step = v.StepDegrees;
            Shapes(s, v);
            _mesh.Clear();
            _mesh.SetVertices(s.Points);
            _mesh.SetColors(s.Colours);
            _mesh.SetTriangles(s.Triangles, 0, false);
        }

        /// <summary>The vertices of the last mesh filled (for tests).</summary>
        public int MeshPoints => _shapes.Points.Count;

        static Color32 C(Rgba c, float alpha = 1f) =>
            new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.R) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.G) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.B) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.A * alpha) * 255f));

        /// <summary>Every shape in part A's order (p_variant.py's draw_P, compose.py's draw_sight, make V), round the sight's middle.</summary>
        void Shapes(SightShapes s, SightValues v)
        {
            Vector2 o = Vector2.zero;
            Color32 dark = C(v.Dark), gun = C(v.Gunmetal), teal = C(v.Teal);
            Color32 line = Now ? C(v.White) : Late ? C(v.LateGrey) : teal;
            float bandHalf = (v.RingOuter - v.RingInner) * 0.5f, mid = v.RingMiddle, half = v.BracketSpan * 0.5f;
            float cap = v.Outline / Mathf.Max(1e-3f, mid) * Mathf.Rad2Deg;
            float lineIn = v.LineRadius - v.LineWidth * 0.5f, lineOut = v.LineRadius + v.LineWidth * 0.5f;
            Vector2 mech = o;
            for (int k = 0; k < 4; k++)
            {
                float ang = k * 90f;
                Vector2 c = SightShapes.Polar(o, Shift, ang);
                s.Band(c, mid - bandHalf - v.Outline, mid + bandHalf + v.Outline, ang - half - cap, ang + half + cap, dark);
                s.Band(c, v.RingInner, v.RingOuter, ang - half, ang + half, gun);
                // Variant P: each bracket's dim teal line, the same in every state; the whole ring spreading (V): the ring's own line.
                s.Band(c, lineIn, lineOut, ang - half, ang + half, PointerStays ? C(v.Teal, v.BracketLineAlpha) : line);
            }
            if (PointerStays)
            {
                // The pointer's ring: the teal line (white at "now", grey too late) on its own thin dark outline, never moved.
                s.Band(o, lineIn - v.PointerOutline, lineOut + v.PointerOutline, 0f, 360f, dark);
                s.Band(o, lineIn, lineOut, 0f, 360f, line);
            }
            else mech = SightShapes.Polar(o, Shift, 90f); // his sheet's sixth drawing: the arm with the right segment

            // The centre dot, on the cursor.
            s.Disc(o, v.DotRadius + v.Outline, dark);
            s.Disc(o, v.DotRadius, teal);

            // The teeth zone: one rack with square teeth pointing in, its outline inside its own edge, ink from ZoneEnd to ZoneStart.
            if (ZoneShown && ZoneStart > ZoneEnd)
            {
                Color32 col = Now ? C(v.Teal, ZoneAlpha) : Late ? C(v.BronzeDim, ZoneAlpha) : C(v.Bronze, ZoneAlpha);
                Teeth(s, v, mech, C(v.Dark, ZoneAlpha), col);
            }

            // The arm's band from the pin to the head, the pin, the head.
            Color32 arm = Late ? C(v.Late) : C(v.Bronze);
            float hc = HeadAngle, trail = hc + v.HeadHalf - 1f;
            if (trail < v.PinAngle)
            {
                s.Band(mech, v.HeadRadius - v.ArmWidth * 0.5f - v.Outline, v.HeadRadius + v.ArmWidth * 0.5f + v.Outline, trail, v.PinAngle, dark);
                s.Band(mech, v.HeadRadius - v.ArmWidth * 0.5f, v.HeadRadius + v.ArmWidth * 0.5f, trail, v.PinAngle, arm);
            }
            Vector2 pin = SightShapes.Polar(mech, v.HeadRadius, v.PinAngle);
            s.Disc(pin, v.PinOuter, dark);
            s.Disc(pin, v.PinRadius, Now ? teal : arm);
            s.Disc(pin, v.PinHole, dark);
            float hh = v.HeadHalf, lead = SightArm.LeadAngle(hc, v);
            _corners[0] = SightShapes.Polar(mech, v.HeadInner, hc + hh);
            _corners[1] = SightShapes.Polar(mech, v.HeadOuter, hc + hh);
            _corners[2] = SightShapes.Polar(mech, v.CatchOuter, hc - hh + v.CatchFlat);
            _corners[3] = SightShapes.Polar(mech, v.CatchOuter, lead);
            _corners[4] = SightShapes.Polar(mech, v.CatchFront, lead);
            _corners[5] = SightShapes.Polar(mech, v.HeadInner, hc - hh);
            s.InkedPolygon(_corners, 6, Now ? C(v.White) : Late ? C(v.Late) : C(v.Bronze), dark, v.Outline);
        }

        /// <summary>The rack and its teeth (part A's one sawtooth shape, as bands: the dark whole, then the colour 1 px inside it).</summary>
        void Teeth(SightShapes s, SightValues v, Vector2 mech, Color32 dark, Color32 col)
        {
            float start = ZoneStart, end = ZoneEnd;
            int count = SightArm.Teeth(start, end, v, out float tooth);
            float ol = v.TeethOutline, join = 1f; // px a tooth reaches into the rack, so no seam shows between them
            float rackMid = (v.RackInner + v.RackOuter) * 0.5f, toothMid = (v.TeethInner + v.RackInner) * 0.5f;
            s.Band(mech, v.RackInner, v.RackOuter, end, start, dark);
            for (int i = 0; i < count; i++)
            {
                float t0 = SightArm.ToothStart(i, count, start, end, tooth);
                s.Band(mech, v.TeethInner, v.RackInner + join, t0 - tooth, t0, dark);
            }
            float inRack = ol / rackMid * Mathf.Rad2Deg, inTooth = ol / toothMid * Mathf.Rad2Deg;
            s.Band(mech, v.RackInner + ol, v.RackOuter - ol, end + inRack, start - inRack, col);
            for (int i = 0; i < count; i++)
            {
                float t0 = SightArm.ToothStart(i, count, start, end, tooth);
                s.Band(mech, v.TeethInner + ol, v.RackInner + ol + join, t0 - tooth + inTooth, t0 - inTooth, col);
            }
        }
    }
}
