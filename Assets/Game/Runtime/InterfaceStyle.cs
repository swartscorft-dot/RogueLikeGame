using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The interface's one skin (decision 0011, Kristian's choices of 2026-11-20; work order 045): kinetic, small, at the
    /// figure. Every piece of the interface asks it, where it draws, whether to draw in the skin and how: its numbers and
    /// palette (<see cref="InterfaceValues"/>, reachable in the Inspector while playing on the object this installs), the
    /// two fonts (Barlow Condensed ExtraBold Italic and SemiBold Italic, SIL Open Font License, in Resources/UI/Fonts), the
    /// skill icons made masks once, the texts of numbers made once, and the few ways it draws: a slanted shape is a
    /// rectangle drawn under a shear of <see cref="GUI.matrix"/> about its own middle (one way, used everywhere), text is
    /// italic by its font and never sheared again.
    ///
    /// The master switch is <see cref="InterfaceValues.Kinetic"/>, on by default: off, every piece draws exactly as it did
    /// before. Only drawing changes: what a piece shows, and when, are its own rules as before.
    ///
    /// The icons: Resources/UI/Icons/&lt;the skill's name without spaces&gt;.bytes, each a PNG of a white sign on black kept as
    /// its bytes (the default import of a PNG cannot be read from code, and a built player must read them too), decoded once,
    /// its brightness made the alpha of a small white mask (<see cref="InterfaceRules.Mask"/>) that the pieces tint. A skill
    /// with no icon shows its key and a short name instead.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="RunHud"/> (never in a test run); without one
    /// the pieces read the shared values (<see cref="Shared"/>), so tests turn the skin on and off there.
    /// </summary>
    public sealed class InterfaceStyle : MonoBehaviour
    {
        static InterfaceValues s_Shared = new InterfaceValues(); // made anew with each Play session (ResetStatics; work order 055)

        /// <summary>The one running now, or null.</summary>
        public static InterfaceStyle Current { get; private set; }

        /// <summary>The numbers and the switch: the shared ones, which the one running shows in the Inspector.</summary>
        public InterfaceValues Values = s_Shared;

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The values every piece reads: the running one's, else the shared ones.</summary>
        public static InterfaceValues V => Current != null && Current.Values != null ? Current.Values : s_Shared;

        /// <summary>The shared values (tests turn the skin on and off here when none is running).</summary>
        public static InterfaceValues Shared => s_Shared;

        /// <summary>The master switch: draw the interface in the kinetic skin.</summary>
        public static bool Kinetic => V.Kinetic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            // The fourth review of 2026-11-21 (work order 055): with domain reload off the values, and the textures made in
            // code, lasted from one Play session to the next; the values are made anew, the textures destroyed.
            s_Shared = new InterfaceValues();
            s_Heavy = s_Light = null;
            s_FontsTried = false;
            ForgetIcons(immediately: true);
            s_Styles.Clear();
            Gone(s_Pips);
            Gone(s_Halftone); // work order 063
            s_Halftone = null;
            Gone(s_ButtonNormal);
            Gone(s_ButtonHover);
            Gone(s_ButtonActive);
            s_Pips = null;
            s_Buttons = null;
            s_ButtonNormal = s_ButtonHover = s_ButtonActive = null;
        }

        /// <summary>A texture made in code, destroyed at once (between Play sessions nothing draws it any more).</summary>
        static void Gone(Object o)
        {
            if (o != null) DestroyImmediate(o);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static InterfaceStyle Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("InterfaceStyle");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<InterfaceStyle>();
            return Current;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------------- The palette ----------------

        public static Color C(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        public static Color C(Rgba c, float alpha) => new Color(c.R, c.G, c.B, c.A * alpha);

        public static Color Ink(UiInk ink)
        {
            InterfaceValues v = V;
            switch (ink)
            {
                case UiInk.Teal: return C(v.Teal);
                case UiInk.Red: return C(v.Red);
                case UiInk.Black: return C(v.Black);
                case UiInk.Grey: return C(v.Grey);
                case UiInk.Gold: return C(v.Gold);
                case UiInk.Marks: return C(v.Marks);
                case UiInk.Dazed: return C(v.Dazed);
                case UiInk.Late: return C(v.Late);
                default: return C(v.White);
            }
        }

        // ---------------- The fonts ----------------

        static Font s_Heavy, s_Light;
        static bool s_FontsTried;

        /// <summary>The heavy condensed italic (the words, numbers and headings) and the lighter one (running text); null if missing.</summary>
        public static Font Heavy
        {
            get
            {
                LoadFonts();
                return s_Heavy;
            }
        }

        public static Font Light
        {
            get
            {
                LoadFonts();
                return s_Light;
            }
        }

        static void LoadFonts()
        {
            if (s_FontsTried && (s_Heavy != null || s_Light != null)) return;
            if (s_FontsTried && Time.frameCount % 120 != 0) return; // missing: not looked for every frame
            s_FontsTried = true;
            s_Heavy = Resources.Load<Font>(V.HeavyFont);
            s_Light = Resources.Load<Font>(V.LightFont);
            if (s_Heavy == null) s_Heavy = s_Light;
            if (s_Light == null) s_Light = s_Heavy;
        }

        static readonly Dictionary<int, GUIStyle> s_Styles = new Dictionary<int, GUIStyle>();

        /// <summary>
        /// A text style in the skin, made once for each font, size and anchor: white (tinted by <see cref="GUI.color"/>), on
        /// one line unless <paramref name="wrap"/>, never clipped.
        /// </summary>
        public static GUIStyle Text(bool heavy, int size, TextAnchor anchor, bool wrap = false)
        {
            int key = (heavy ? 1 : 0) | (wrap ? 2 : 0) | ((int)anchor << 2) | (Mathf.Clamp(size, 1, 4000) << 6);
            if (s_Styles.TryGetValue(key, out GUIStyle style) && style.font == (heavy ? Heavy : Light)) return style;
            style = new GUIStyle
            {
                font = heavy ? Heavy : Light, fontSize = Mathf.Max(1, size), alignment = anchor, wordWrap = wrap,
                clipping = wrap ? TextClipping.Clip : TextClipping.Overflow, richText = false,
            };
            style.normal.textColor = Color.white;
            s_Styles[key] = style;
            return style;
        }

        // ---------------- The icons ----------------

        static readonly Dictionary<string, Texture2D> s_Icons = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> s_Missing = new HashSet<string>();
        // The fourth review of 2026-11-21 (work order 055): every icon on disk is decoded at once, when a run starts or the
        // skill bar wakes (LoadIcons), not in the frame one is first drawn; by its file's name.
        static readonly Dictionary<string, Texture2D> s_IconFiles = new Dictionary<string, Texture2D>();
        static bool s_IconsLoaded;

        /// <summary>The icon of a skill (or "Dash", "Focus"), a white mask to tint; null when it has none (key and name instead).</summary>
        public static Texture2D Icon(string skillName)
        {
            if (string.IsNullOrEmpty(skillName) || s_Missing.Contains(skillName)) return null;
            if (s_Icons.TryGetValue(skillName, out Texture2D icon) && icon != null) return icon;
            LoadIcons(); // already done but in a test that forgot them
            s_IconFiles.TryGetValue(InterfaceRules.IconName(skillName), out icon);
            if (icon == null) s_Missing.Add(skillName); // a missing one is remembered as missing, not looked for again
            else s_Icons[skillName] = icon;
            return icon;
        }

        /// <summary>
        /// Decodes every icon in the icons' folder, once (work order 055): from the skill bar's waking and a run's start, so no
        /// frame of play decodes one. Returns how many there are.
        /// </summary>
        public static int LoadIcons()
        {
            if (s_IconsLoaded) return s_IconFiles.Count;
            s_IconsLoaded = true;
            string folder = V.IconFolder.TrimEnd('/');
            foreach (TextAsset bytes in Resources.LoadAll<TextAsset>(folder))
            {
                if (bytes == null || s_IconFiles.ContainsKey(bytes.name)) continue;
                Texture2D mask = MakeMask(bytes);
                if (mask != null) s_IconFiles[bytes.name] = mask;
            }
            return s_IconFiles.Count;
        }

        /// <summary>The icons decoded and held now. For tests.</summary>
        public static int IconsLoaded
        {
            get
            {
                int n = 0;
                foreach (Texture2D t in s_IconFiles.Values) if (t != null) n++;
                return n;
            }
        }

        /// <summary>How many icons have been decoded since the game started (work order 055: equipping a skill decodes none). For tests.</summary>
        public static int IconsDecoded { get; private set; }

        /// <summary>Forgets the icons made, so they are loaded again (for tests that take one away).</summary>
        public static void ForgetIcons() => ForgetIcons(immediately: false);

        static void ForgetIcons(bool immediately)
        {
            foreach (Texture2D t in s_IconFiles.Values)
                if (t != null)
                {
                    if (immediately) DestroyImmediate(t);
                    else Destroy(t);
                }
            s_IconFiles.Clear();
            s_IconsLoaded = false;
            s_Icons.Clear();
            s_Missing.Clear();
        }

        /// <summary>
        /// An icon's PNG bytes from Resources, decoded once, made a white mask whose alpha is the picture's brightness,
        /// averaged down to <see cref="InterfaceValues.IconMaskSize"/> square. Null when it is not there or cannot be read.
        /// </summary>
        static Texture2D MakeMask(TextAsset bytes)
        {
            if (bytes == null) return null;
            string name = bytes.name;
            IconsDecoded++;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(bytes.bytes, false) || source.width < 8 || source.height < 8) return null;
                Color32[] pixels = source.GetPixels32();
                var grey = new byte[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) grey[i] = InterfaceRules.Brightness(pixels[i].r, pixels[i].g, pixels[i].b);
                int size = Mathf.Clamp(V.IconMaskSize, 8, 256);
                byte[] alpha = InterfaceRules.Mask(grey, source.width, source.height, size);
                var mask = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                };
                var cells = new Color32[size * size];
                for (int i = 0; i < cells.Length; i++) cells[i] = new Color32(255, 255, 255, alpha[i]);
                mask.SetPixels32(cells);
                mask.Apply(true, true);
                return mask;
            }
            finally
            {
                Destroy(source);
                Resources.UnloadAsset(bytes);
            }
        }

        // ---------------- Texts made once ----------------

        static readonly string[] s_Numbers = new string[10000];
        static readonly string[] s_Plus = new string[1000];
        static readonly string[] s_Cooldowns = new string[10000];

        /// <summary>A whole number's text, made once (0 to 9999; others each time).</summary>
        public static string Number(int n)
        {
            if (n < 0 || n >= s_Numbers.Length) return n.ToString(CultureInfo.InvariantCulture);
            return s_Numbers[n] ?? (s_Numbers[n] = n.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>"+8": health back, made once.</summary>
        public static string Plus(int n)
        {
            if (n < 0 || n >= s_Plus.Length) return "+" + n.ToString(CultureInfo.InvariantCulture);
            return s_Plus[n] ?? (s_Plus[n] = "+" + Number(n));
        }

        /// <summary>A cooldown's number as the strip shows it (<see cref="InterfaceRules.CooldownText"/>), made once.</summary>
        public static string Cooldown(float seconds)
        {
            int tenths = InterfaceRules.CooldownTenths(seconds);
            if (tenths <= 0) return "";
            if (tenths >= s_Cooldowns.Length) return InterfaceRules.CooldownText(seconds);
            return s_Cooldowns[tenths] ?? (s_Cooldowns[tenths] = InterfaceRules.CooldownText(seconds));
        }

        // ---------------- Drawing (inside OnGUI, on the repaint) ----------------

        static readonly GUIContent s_Content = new GUIContent();

        /// <summary>The layout's scale now, real pixels per layout pixel.</summary>
        public static float Scale => InterfaceRules.Scale(Screen.height, V);

        /// <summary>Sets the matrix to the layout's scale; returns the view's width in layout pixels.</summary>
        public static float BeginLayout(out Matrix4x4 before)
        {
            before = GUI.matrix;
            float scale = Mathf.Max(1e-4f, Scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            return Screen.width / scale;
        }

        /// <summary>A plain rectangle.</summary>
        public static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        /// <summary>A slanted shape: the rectangle leaning right by <see cref="InterfaceValues.Slant"/>, about its own middle.</summary>
        public static void Slanted(Rect r, Color c) => Slanted(r, r.center.y, c);

        /// <summary>A slanted shape leaning about the line <paramref name="pivotY"/>, so shapes on one line share their slant.</summary>
        public static void Slanted(Rect r, float pivotY, Color c)
        {
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before * Shear(pivotY, V.Slant);
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.matrix = before;
        }

        /// <summary>A slanted bar: its back, and its fill from the left, its cut end slanted as its ends are.</summary>
        public static void SlantedBar(Rect r, float fill, Color back, Color front)
        {
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before * Shear(r.center.y, V.Slant);
            if (back.a > 0f)
            {
                GUI.color = back;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
            fill = Mathf.Clamp01(fill);
            if (fill > 0f)
            {
                GUI.color = front;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * fill, r.height), Texture2D.whiteTexture);
            }
            GUI.matrix = before;
        }

        /// <summary>A slanted outline (a frame of four thin sides) round a rectangle.</summary>
        public static void SlantedFrame(Rect r, Color c, float t) => SlantedFrame(r, r.center.y, c, t);

        /// <summary>A slanted outline leaning about the line <paramref name="pivotY"/>.</summary>
        public static void SlantedFrame(Rect r, float pivotY, Color c, float t)
        {
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before * Shear(pivotY, V.Slant);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.matrix = before;
        }

        /// <summary>
        /// The dark plate behind a slanted box (work order 063, item 4: "for flat"): under the same slant about
        /// <paramref name="pivotY"/>, moved down and right by <paramref name="depth"/> and larger by <paramref name="outline"/>
        /// on every side, in <see cref="InterfaceValues.DepthInk"/>; drawn before the box. Nothing when both are 0 (this
        /// morning's flat box). A new helper: none of the others changed.
        /// </summary>
        public static void Plate(Rect r, float pivotY, float depth, float outline)
        {
            if (depth <= 0f && outline <= 0f) return;
            depth = Mathf.Max(0f, depth);
            outline = Mathf.Max(0f, outline);
            Slanted(new Rect(r.x - outline + depth, r.y - outline + depth, r.width + 2f * outline, r.height + 2f * outline), pivotY, C(V.DepthInk));
        }

        /// <summary>The shear: x moves right by <paramref name="slant"/> per unit above <paramref name="pivotY"/>.</summary>
        public static Matrix4x4 Shear(float pivotY, float slant)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m01 = -slant;
            m.m03 = slant * pivotY;
            return m;
        }

        /// <summary>Text in a style, tinted, with one dark copy under it when <paramref name="shadow"/> (never sheared: the font is italic).</summary>
        public static void Label(Rect r, string text, GUIStyle style, Color c, bool shadow = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            s_Content.text = text;
            if (shadow)
            {
                float o = V.ShadowOffset;
                GUI.color = C(V.Shadow, c.a);
                GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), s_Content, style);
            }
            GUI.color = c;
            GUI.Label(r, s_Content, style);
        }

        /// <summary>The size a text takes in a style.</summary>
        public static Vector2 Measure(string text, GUIStyle style)
        {
            s_Content.text = text ?? "";
            return style.CalcSize(s_Content);
        }

        /// <summary>The height a text takes in a style, wrapped at this width (work order 055: a card measures its words).</summary>
        public static float MeasureHeight(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            s_Content.text = text;
            return style.CalcHeight(s_Content, width);
        }

        /// <summary>A thin teal rule, the screens' one line of accent.</summary>
        public static void Rule(float x, float y, float width) => Slanted(new Rect(x, y, width, V.RuleThickness), C(V.Teal));

        /// <summary>A key on a small white slanted plate, the key in black (the type sheet's keys).</summary>
        public static void KeyPlate(Rect r, string key, int size)
        {
            Slanted(r, C(V.White));
            Label(r, key, Text(true, size, TextAnchor.MiddleCenter), C(V.Black));
        }

        /// <summary>A prompt in the skin: the key on its plate and the words after it, over a faint slanted back. Returns its width.</summary>
        public static float Prompt(float centerX, float centerY, string key, string words)
        {
            InterfaceValues v = V;
            GUIStyle style = Text(true, v.PromptSize, TextAnchor.MiddleLeft);
            float wordsWidth = Measure(words, style).x;
            float h = v.PromptSize * 1.45f, keyW = Mathf.Max(v.PromptKeyWidth, Measure(key, style).x + 10f);
            float w = keyW + 8f + wordsWidth + 12f;
            var back = new Rect(centerX - w * 0.5f, centerY - h * 0.5f, w, h);
            Slanted(back, C(v.Back));
            if (!string.IsNullOrEmpty(key)) KeyPlate(new Rect(back.x + 3f, back.y + 3f, keyW, h - 6f), key, v.PromptSize - 3);
            Label(new Rect(back.x + keyW + 9f, back.y, wordsWidth + 4f, h), words, style, C(v.White), true);
            return w;
        }

        /// <summary>A line of words in the skin over a faint slanted back with a teal rule under it (the floor-cleared line, a notice).</summary>
        public static void Line(float centerX, float top, string words)
        {
            InterfaceValues v = V;
            GUIStyle style = Text(true, v.PromptSize, TextAnchor.MiddleCenter);
            float w = Measure(words, style).x + 30f, h = v.PromptSize * 1.6f;
            var r = new Rect(centerX - w * 0.5f, top, w, h);
            Slanted(r, C(v.Back));
            Rule(r.x + 6f, r.yMax - v.RuleThickness, r.width - 12f);
            Label(r, words, style, C(v.White), true);
        }

        // ---------------- Textures made in code ----------------

        static Texture2D s_Pips;
        public const int PipCell = 14;  // the diamond pips' texture: a 10-texel diamond and a 4-texel gap, repeated along a row
        public const int PipLit = 10;

        /// <summary>A row of tiny diamonds, one texture repeated along the row (the marks over an enemy): white, to tint.</summary>
        public static Texture2D Pips
        {
            get
            {
                if (s_Pips != null) return s_Pips;
                s_Pips = new Texture2D(PipCell, PipLit, TextureFormat.RGBA32, false)
                {
                    name = "KineticPips", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                var px = new Color32[PipCell * PipLit];
                float c = (PipLit - 1) * 0.5f;
                for (int y = 0; y < PipLit; y++)
                    for (int x = 0; x < PipCell; x++)
                    {
                        float d = x < PipLit ? (Mathf.Abs(x - c) + Mathf.Abs(y - c)) / (c + 0.5f) : 2f;
                        byte a = (byte)(Mathf.Clamp01((1f - d) * 4f + 0.5f) * 255f);
                        px[y * PipCell + x] = new Color32(255, 255, 255, a);
                    }
                s_Pips.SetPixels32(px);
                s_Pips.Apply();
                return s_Pips;
            }
        }

        static Texture2D s_Halftone;
        /// <summary>The halftone patch's texture: this many cells of this many texels a side.</summary>
        public const int HalftoneCells = 8, HalftoneCell = 8;

        /// <summary>
        /// A patch of halftone dots (work order 063, item 4: the pictures' small accent), large at its top left and shrinking
        /// along the diagonal to nothing at its bottom right: white, to tint. Made once in code.
        /// </summary>
        public static Texture2D Halftone
        {
            get
            {
                if (s_Halftone != null) return s_Halftone;
                int n = HalftoneCells * HalftoneCell;
                s_Halftone = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    name = "KineticHalftone", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int cx = x / HalftoneCell, cy = (n - 1 - y) / HalftoneCell; // rows come from the bottom: cy 0 is the top row
                        float t = (cx + cy) / (2f * (HalftoneCells - 1));      // 0 at the top left, 1 at the bottom right
                        float radius = (1f - t) * HalftoneCell * 0.55f;
                        float dx = x % HalftoneCell + 0.5f - HalftoneCell * 0.5f, dy = y % HalftoneCell + 0.5f - HalftoneCell * 0.5f;
                        float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                s_Halftone.SetPixels32(px);
                s_Halftone.Apply();
                return s_Halftone;
            }
        }

        /// <summary>A row of <paramref name="count"/> diamonds in one call, the last gap cut off.</summary>
        public static void PipRow(Rect r, int count, Color color)
        {
            if (count <= 0) return;
            GUI.color = color;
            float u = count - (PipCell - PipLit) / (float)PipCell;
            GUI.DrawTextureWithTexCoords(r, Pips, new Rect(0f, 0f, u, 1f));
        }

        static GUIStyle s_Buttons;
        static Texture2D s_ButtonNormal, s_ButtonHover, s_ButtonActive;

        /// <summary>A screen's button in the skin: flat dark, teal on hover, the lighter italic, left aligned.</summary>
        public static GUIStyle Button(int size)
        {
            if (s_Buttons == null || s_ButtonNormal == null)
            {
                s_ButtonNormal = Flat(new Color(1f, 1f, 1f, 0.07f), "KineticButton");
                s_ButtonHover = Flat(new Color(V.Teal.R, V.Teal.G, V.Teal.B, 0.28f), "KineticButtonHover");
                s_ButtonActive = Flat(new Color(V.Teal.R, V.Teal.G, V.Teal.B, 0.5f), "KineticButtonActive");
                s_Buttons = new GUIStyle
                {
                    font = Light, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4), wordWrap = true, clipping = TextClipping.Clip,
                };
                s_Buttons.normal.background = s_ButtonNormal;
                s_Buttons.hover.background = s_ButtonHover;
                s_Buttons.active.background = s_ButtonActive;
                s_Buttons.focused.background = s_ButtonNormal;
                s_Buttons.normal.textColor = s_Buttons.hover.textColor = s_Buttons.active.textColor = s_Buttons.focused.textColor = Color.white;
            }
            if (s_Buttons.fontSize != size) s_Buttons.fontSize = size;
            return s_Buttons;
        }

        static Texture2D Flat(Color c, string name)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
