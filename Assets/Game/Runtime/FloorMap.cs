using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The small map in the corner (work order 041; Kristian's second session of 2026-11-20: "It is not quite intuitive
    /// where the chest rooms are; I end up double-checking several rooms for them", and his answers: a small map in the
    /// corner, and opened chests and cleared rooms shown). IMGUI in the HUD's look (<see cref="RunHud"/>: a dark box, the
    /// game's cyan, laid out for a view 720 pixels high and scaled), at the top right beside the run box, about a sixth of
    /// the view high (<see cref="DoorSignValues.MapHeightShare"/>). Held, <see cref="LargeKey"/> (Tab) shows it large over
    /// the middle of the view; the pause menu's list of keys names it.
    ///
    /// What it shows is <see cref="FloorMapRules"/>: the floor's rooms as their outlines, to scale (the whole floor's extent
    /// fixes the scale, so nothing moves as rooms come into view), and the doors between the rooms it shows. The room she
    /// is in is filled in the cyan and a white dot is where she is; a room she has been in is filled grey, faintly once it
    /// is done; a room she has not been in shows only as an outline, once the room before it has been entered. A treasure
    /// room, the elite room and the exit room show their door's colour and sign (<see cref="DoorSignRules"/>) until they are
    /// done; done, a treasure room loses its gold and the others' go dim. Work order 051: a carved room shows its real
    /// outline (<see cref="RoomShapes.Outline"/>) and only its open cells filled (<see cref="RoomShapes.OpenRects"/>).
    ///
    /// It is not drawn under a choice, an end screen (from the moment the run ends), the pause menu or the screen between
    /// runs, nor outside a floor, nor with <see cref="LevelValues.FloorMap"/> off. What it shows is worked out in
    /// <see cref="Tick"/> (LateUpdate, and tests) into arrays made once a floor, and drawn from them on the repaint only:
    /// nothing is allocated in a frame. The signs' little pictures are textures made in code from the signs' shapes, once.
    /// It installs itself when Play starts under the same rule as <see cref="GeneratedLevels"/> (never in a test run);
    /// tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed class FloorMap : MonoBehaviour
    {
        /// <summary>
        /// Held, the map shows large (work order 041). Tab: bound to nothing in <see cref="InputBindings"/> and no developer
        /// key; the trial's Second Look has a key of its own since work order 047 (<see cref="GeneratedLevels.RedrawKey"/>).
        /// </summary>
        public const Key LargeKey = Key.Tab;

        /// <summary>The run box's width in the HUD and the gap beside it: the small map stands to its left.</summary>
        const float BesideRunBox = 260f + 18f;
        const float Margin = 10f;
        const float Pad = 6f;

        static readonly Color s_Accent = new Color(0.25f, 0.85f, 0.85f, 1f);
        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color s_Entered = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color s_Done = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color s_Outline = new Color(1f, 1f, 1f, 0.75f);
        static readonly Color s_AheadOutline = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color s_Door = new Color(1f, 1f, 1f, 0.95f);

        /// <summary>The one running now, or null.</summary>
        public static FloorMap Current { get; private set; }

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>For tests: as if <see cref="LargeKey"/> were held.</summary>
        public bool HoldLarge { get; set; }

        /// <summary>The map is drawn now (in a floor, the switch on, nothing over it).</summary>
        public bool Shown { get; private set; }
        /// <summary>It is drawn large now.</summary>
        public bool Large { get; private set; }
        /// <summary>The room she is in, by its place in the run; -1 for none.</summary>
        public int Here { get; private set; } = -1;
        /// <summary>Where the dot is, as a point of the floor (metres).</summary>
        public LevelPoint Dot { get; private set; }
        /// <summary>The floor it shows, or null.</summary>
        public LevelLayout Layout => _layout;
        /// <summary>Where the small map stands, in the 720-high layout (top right).</summary>
        public Rect SmallRect { get; private set; }
        /// <summary>Where the large one stands, in the same layout (the middle).</summary>
        public Rect LargeRect { get; private set; }

        /// <summary>How a room shows now, by its place in the run.</summary>
        public MapShown RoomShown(int room) => _shown != null && room >= 0 && room < _shown.Length ? _shown[room] : MapShown.Hidden;
        /// <summary>The room is done now (its chest opened, or cleared).</summary>
        public bool RoomDone(int room) => _done != null && room >= 0 && room < _done.Length && _done[room];
        /// <summary>Its fill is dim now.</summary>
        public bool RoomDim(int room) => FloorMapRules.Dim(RoomShown(room), RoomDone(room));
        /// <summary>Its sign's strength now: 1 lit, a share dim, 0 none.</summary>
        public float SignStrength(int room) => _strength != null && room >= 0 && room < _strength.Length ? _strength[room] : 0f;
        /// <summary>What it fills for a room (work order 051): the rectangles of its open cells.</summary>
        public IReadOnlyList<CellRect> FillsOf(int room) => _fills != null && room >= 0 && room < _fills.Length ? _fills[room] : System.Array.Empty<CellRect>();
        /// <summary>The outline it draws for a carved room (work order 051); empty for a room drawn as its rectangle.</summary>
        public IReadOnlyList<RoomShapes.Edge> OutlineOf(int room) =>
            _edges != null && room >= 0 && room < _edges.Length && _edges[room] != null ? _edges[room] : System.Array.Empty<RoomShapes.Edge>();
        /// <summary>Its sign.</summary>
        public DoorSign SignOf(int room) => _sign != null && room >= 0 && room < _sign.Length ? _sign[room] : DoorSign.None;
        /// <summary>The door of the layout's list shows now.</summary>
        public bool DoorShown(int door) => _layout != null && door >= 0 && door < _layout.Doors.Count
                                         && FloorMapRules.DoorShown(RoomShown(_layout.Doors[door].RoomA), RoomShown(_layout.Doors[door].RoomB));

        LevelLayout _layout;
        GeneratedLevel _level;
        DoorSignValues _v;
        MapShown[] _shown;
        bool[] _done;
        float[] _strength;
        DoorSign[] _sign;
        Color[] _colour;
        int[] _before;
        CellRect[][] _fills;          // work order 051: each room's open cells as rectangles
        RoomShapes.Edge[][] _edges;   // and a carved room's outline (null for a room that is its rectangle)
        double _smallScale, _largeScale;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;
        static readonly Dictionary<DoorSign, Texture2D> s_Icons = new Dictionary<DoorSign, Texture2D>();
        static Texture2D s_Dot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            s_Icons.Clear();
            s_Dot = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static FloorMap Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("FloorMap");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<FloorMap>();
            return Current;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void LateUpdate() => Tick();

        /// <summary>Works out what the map shows now, from the run's state. Allocates nothing but once a floor.</summary>
        public void Tick()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            if (level == null || level.Layout == null)
            {
                Shown = Large = false;
                Here = -1;
                _level = null;
                _layout = null;
                return;
            }
            if (level != _level || level.Layout != _layout) NewFloor(level);
            LevelValues values = level.Values;
            Shown = values.FloorMap && levels.Choice == GeneratedLevels.ChoiceKind.None && !levels.Paused
                    && levels.Ending == GeneratedLevels.EndKind.None && !levels.BetweenRunsUp;
            bool held = HoldLarge || (PlayerInputDriver.ReadRealDevices && Keyboard.current is Keyboard keyboard && keyboard[LargeKey].isPressed);
            Large = Shown && held;

            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            if (_player != null) Dot = level.ToLevel(_player.Position);
            LevelRoom here = levels.CurrentRoom;
            Here = here != null ? here.Order : -1;
            ICollection<int> entered = levels.RoomsEntered as ICollection<int>;
            ICollection<int> opened = levels.ChestsOpened as ICollection<int>;
            List<LevelRoom> rooms = _layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                LevelRoom r = rooms[i];
                bool beforeEntered = _before[i] >= 0 && entered != null && entered.Contains(_before[i]);
                _shown[i] = FloorMapRules.Shown(i == Here, entered != null && entered.Contains(i), beforeEntered);
                _done[i] = DoorSignRules.Done(r.Kind, opened != null && opened.Contains(i), Cleared(i));
                _strength[i] = FloorMapRules.SignStrength(_sign[i], _shown[i], r.Kind, _done[i], _v);
            }
            Place();
        }

        /// <summary>Where the two maps stand, in the 720-high layout of the view as it is now (1280 by 720 without a window).</summary>
        void Place()
        {
            float height = InterfaceStyle.ScreenHeight > 0 ? InterfaceStyle.ScreenHeight : SkillBar.LayoutHeight;
            float viewWidth = (InterfaceStyle.ScreenWidth > 0 ? InterfaceStyle.ScreenWidth : 1280f) * SkillBar.LayoutHeight / height;
            CellRect box = _layout.Bounds();
            double cs = _layout.CellSize;
            FloorMapRules.Fit(box.Width * cs, box.Height * cs, SkillBar.LayoutHeight * _v.MapHeightShare - 2 * Pad, _v.MapWidthMost - 2 * Pad,
                              out double w, out double h, out _smallScale);
            SmallRect = new Rect(viewWidth - Margin - BesideRunBox - (float)w - 2 * Pad, Margin, (float)w + 2 * Pad, (float)h + 2 * Pad);
            FloorMapRules.Fit(box.Width * cs, box.Height * cs, SkillBar.LayoutHeight * _v.LargeHeightShare, viewWidth * 0.8, out w, out h, out _largeScale);
            LargeRect = new Rect((viewWidth - (float)w) * 0.5f - Pad, (SkillBar.LayoutHeight - (float)h) * 0.5f - Pad, (float)w + 2 * Pad, (float)h + 2 * Pad);
        }

        void NewFloor(GeneratedLevel level)
        {
            _level = level;
            _layout = level.Layout;
            _v = level.Values.Signs ?? new DoorSignValues();
            int n = _layout.Rooms.Count;
            _shown = new MapShown[n];
            _done = new bool[n];
            _strength = new float[n];
            _sign = new DoorSign[n];
            _colour = new Color[n];
            _before = new int[n];
            _fills = new CellRect[n][];
            _edges = new RoomShapes.Edge[n][];
            for (int i = 0; i < n; i++)
            {
                LevelRoom r = _layout.Rooms[i];
                // Work order 051: a carved room shows its outline as it is, its open cells filled.
                _fills[i] = RoomShapes.OpenRects(r).ToArray();
                _edges[i] = r.SolidCount > 0 ? RoomShapes.Outline(r).ToArray() : null;
                _sign[i] = DoorSignRules.SignOf(r, _layout.Floor, level.Values.FloorsPerAct);
                _colour[i] = CryptKit.Picked(DoorSignRules.ColourOf(_sign[i], _v));
                _before[i] = FloorMapRules.Before(_layout, r);
            }
        }

        bool Cleared(int order)
        {
            if (!_level.EnemiesByRoom.TryGetValue(order, out List<Enemy> list) || list.Count == 0) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) return false;
            return true;
        }

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (!Shown || _layout == null || Event.current.type != EventType.Repaint) return;
            GUI.depth = InterfaceDepth.Fight; // work order 068: under an offer and every screen
            float scale = InterfaceStyle.ScreenHeight / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            Color colourBefore = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Draw(SmallRect, _smallScale, 1f);
            if (Large) Draw(LargeRect, _largeScale, 2f);

            GUI.color = colourBefore;
            GUI.matrix = matrixBefore;
        }

        /// <summary>True when the map is drawn in the interface's kinetic skin (work order 045): thin outlines, no box behind it.</summary>
        public bool Kinetic => InterfaceStyle.Kinetic;

        void Draw(Rect frame, double scale, float line)
        {
            bool kinetic = Kinetic;
            Rules.InterfaceValues v = InterfaceStyle.V;
            if (kinetic)
            {
                // Work order 045: no box behind it, a thin slanted frame round it; a large map keeps a faint back to read over the floor.
                if (line > 1f) InterfaceStyle.Slanted(frame, InterfaceStyle.C(v.Back, 0.8f));
                InterfaceStyle.SlantedFrame(frame, InterfaceStyle.C(v.White, 0.35f), v.MapFrameLine * line * 0.75f);
                InterfaceStyle.Slanted(new Rect(frame.x, frame.y - 3f, frame.width * 0.35f, 2f), InterfaceStyle.C(v.Teal));
                line *= v.MapLine;
            }
            else
            {
                Fill(frame, s_Back);
                Fill(new Rect(frame.x, frame.y, 3f, frame.height), s_Accent);
            }
            CellRect box = _layout.Bounds();
            double cs = _layout.CellSize;
            float x0 = frame.x + Pad, y0 = frame.y + Pad;
            Rect Of(CellRect c) => new Rect(x0 + (float)((c.X - box.X) * cs * scale), y0 + (float)((box.Top - c.Top) * cs * scale),
                                            (float)(c.Width * cs * scale), (float)(c.Height * cs * scale));

            List<LevelRoom> rooms = _layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                MapShown shown = _shown[i];
                if (shown == MapShown.Hidden) continue;
                Rect r = Of(rooms[i].Rect);
                Color fill = Color.clear;
                if (kinetic)
                {
                    // Work order 045: the room she is in filled teal, a room she has been in faintly white, dimmer once done.
                    if (shown == MapShown.Here) fill = InterfaceStyle.C(v.Teal, v.MapHereFill);
                    else if (shown == MapShown.Entered) fill = InterfaceStyle.C(v.White, _done[i] ? v.MapEnteredFill * 0.4f : v.MapEnteredFill);
                }
                else if (shown == MapShown.Here) fill = new Color(s_Accent.r, s_Accent.g, s_Accent.b, 0.5f);
                else if (shown == MapShown.Entered) fill = _done[i] ? s_Done : s_Entered;
                // Work order 051: a carved room filled cell rectangle by cell rectangle, as its outline is.
                if (fill.a > 0f)
                    foreach (CellRect c in _fills[i]) Fill(Of(c), fill);
                float k = _strength[i];
                Color edge = shown == MapShown.Ahead ? s_AheadOutline : s_Outline;
                if (k > 0f)
                {
                    Color c = _colour[i];
                    edge = new Color(c.r * k + (1f - k) * 0.4f, c.g * k + (1f - k) * 0.4f, c.b * k + (1f - k) * 0.4f, k >= 1f ? 1f : 0.7f);
                }
                if (_edges[i] == null) Frame(r, edge, line);
                else
                    foreach (RoomShapes.Edge e in _edges[i])
                    {
                        // Work order 051: the carved outline, a line along each stretch of it, inside the room as the frame is.
                        float ex = x0 + (float)((e.Line - box.X) * cs * scale), ey = y0 + (float)((box.Top - e.Line) * cs * scale);
                        Fill(e.Vertical
                                 ? new Rect(ex - line * 0.5f, y0 + (float)((box.Top - e.To) * cs * scale), line, (float)((e.To - e.From) * cs * scale))
                                 : new Rect(x0 + (float)((e.From - box.X) * cs * scale), ey - line * 0.5f, (float)((e.To - e.From) * cs * scale), line), edge);
                    }
                if (k > 0f)
                {
                    Texture2D icon = Icon(_sign[i]);
                    float size = Mathf.Min(Mathf.Min(r.width, r.height) * 0.62f, 22f * line);
                    GUI.color = new Color(edge.r, edge.g, edge.b, k >= 1f ? 1f : 0.6f);
                    GUI.DrawTexture(new Rect(r.center.x - size * 0.5f, r.center.y - size * 0.5f, size, size), icon);
                }
            }
            // The doors between the rooms it shows: a bright mark across the wall.
            for (int d = 0; d < _layout.Doors.Count; d++)
            {
                LevelDoor door = _layout.Doors[d];
                if (!FloorMapRules.DoorShown(RoomShown(door.RoomA), RoomShown(door.RoomB))) continue;
                float t = 2f * line, along = (float)(door.Width * cs * scale);
                Rect mark = door.Vertical
                    ? new Rect(x0 + (float)((door.Line - box.X) * cs * scale) - t, y0 + (float)((box.Top - door.From - door.Width) * cs * scale), 2f * t, along)
                    : new Rect(x0 + (float)((door.From - box.X) * cs * scale), y0 + (float)((box.Top - door.Line) * cs * scale) - t, along, 2f * t);
                Fill(mark, s_Door);
            }
            // Where she is.
            if (Here >= 0)
            {
                float dot = 4f * line;
                float px = x0 + (float)((Dot.X / cs - box.X) * cs * scale), py = y0 + (float)((box.Top - Dot.Y / cs) * cs * scale);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(px - dot * 0.5f, py - dot * 0.5f, dot, dot), DotTexture());
            }
            GUI.color = Color.white;
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>A sign's little picture: white where its shape is, four samples a texel, made once.</summary>
        static Texture2D Icon(DoorSign sign)
        {
            if (s_Icons.TryGetValue(sign, out Texture2D t) && t != null) return t;
            const int size = 32;
            List<double[]> parts = DoorSignRules.ShapeOf(sign);
            t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MapSign_" + sign };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        double u = -1 + 2 * (x + 0.25 + 0.5 * (s & 1)) / size, v = -1 + 2 * (y + 0.25 + 0.5 * (s >> 1)) / size;
                        if (DoorSignRules.Inside(parts, u, v)) hits++;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                }
            t.SetPixels32(pixels);
            t.Apply();
            s_Icons[sign] = t;
            return t;
        }

        static Texture2D DotTexture()
        {
            if (s_Dot != null) return s_Dot;
            const int size = 16;
            s_Dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MapDot" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - d) * 4f) * 255f));
                }
            s_Dot.SetPixels32(pixels);
            s_Dot.Apply();
            return s_Dot;
        }
    }
}
