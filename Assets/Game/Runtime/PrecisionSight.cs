using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Work order 143's cone, shown (the order: "the cone is always SHOWN at the cursor"; research r4: random misses with the cursor on
    /// the target are what players hate most, and the camera hides their cause): at the cursor, the cone's real width at the cursor's
    /// distance on the ground (2 × d × tan(half)), opening and closing as two plain brackets either side of the cursor, perpendicular
    /// to the aim (<see cref="DrawBrackets"/>; the reticle he picked, round 1's sheet 2, replaces them in work order 144 by reading
    /// <see cref="Cone"/>, the one entry, and nothing of the rules). In the practice room only, the cone on the ground and a small
    /// speed bar under her with the accurate share's line. A soft tick as she crosses into accuracy, and a miss the cone caused
    /// sounds as a bullet's whizz past, not as a wall's hit.
    ///
    /// Nothing snaps to an enemy: it reads her, the cursor and the camera only. Shown only while precision is in use
    /// (<see cref="PlayerCharacter.PrecisionModeNow"/>), she is alive and no screen holds the game. It installs itself when Play
    /// starts under the same rule as the other pieces (never in a test run); tests make one with <see cref="Ensure"/>. Nothing is
    /// allocated in a frame once its sounds are made.
    /// </summary>
    [DefaultExecutionOrder(Order)]
    public sealed class PrecisionSight : MonoBehaviour
    {
        /// <summary>Its place in the frame: after the camera has moved and the timing cue has measured its sight.</summary>
        public const int Order = TimingAtTheSight.Order + 10;
        const float LayoutHeight = 1080f; // px: the sizes are given for a screen this high

        public static PrecisionSight Current { get; private set; }

        /// <summary>The numbers (reachable in the Inspector while playing).</summary>
        public PrecisionSightValues Values = new PrecisionSightValues();

        /// <summary>The cone at the cursor this frame: the one thing the drawing reads.</summary>
        public ConeAtTheCursor Cone { get; private set; }
        /// <summary>True when the practice room's extras (the cone on the ground, the speed bar) are shown this frame.</summary>
        public bool PracticeExtras { get; private set; }
        /// <summary>Ticks and whizzes played since it started (tests listen here, not to the speakers).</summary>
        public int Ticks { get; private set; }
        public int Whizzes { get; private set; }
        public bool StartedByItself { get; private set; }

        PlayerCharacter _player;
        PlayerHealth _health;
        bool _accurateBefore = true;
        float _tickAt = float.NegativeInfinity;
        int _missesSeen = -1;
        AudioSource _voice;
        AudioClip _tick, _whizz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!PrecisionValues.On || !SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static PrecisionSight Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("PrecisionSight");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<PrecisionSight>();
            return Current;
        }

        void OnDestroy()
        {
            if (_tick != null) Destroy(_tick);
            if (_whizz != null) Destroy(_whizz);
            if (Current == this) Current = null;
        }

        void LateUpdate() => Tick();

        /// <summary>The frame's work: the cone measured, the sounds. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
                _missesSeen = _player != null ? _player.ConeMisses : -1;
            }
            PlayerCharacter p = _player;
            bool on = p != null && p.PrecisionModeNow != PrecisionMode.Off && (_health == null || _health.IsAlive) && !PlayerSigns.ScreenUp;
            PracticeExtras = on && InThePracticeRoom();
            Cone = on ? Measure(p, Camera.main) : default;
            if (p == null) return;
            // The sounds: a soft tick as she crosses into accuracy; a whizz for a miss the cone caused.
            bool accurate = !on || p.PrecisionCone <= 0f;
            float now = Time.time;
            if (on && accurate && !_accurateBefore && now - _tickAt >= Values.TickGap)
            {
                _tickAt = now;
                Ticks++;
                Play(ref _tick, PrecisionSounds.Tick(), "PrecisionTick", Values.TickVolume);
            }
            _accurateBefore = accurate;
            if (p.ConeMisses != _missesSeen)
            {
                if (on && _missesSeen >= 0 && p.ConeMisses > _missesSeen)
                {
                    Whizzes++;
                    Play(ref _whizz, PrecisionSounds.Whizz(), "PrecisionWhizz", Values.WhizzVolume);
                }
                _missesSeen = p.ConeMisses;
            }
        }

        /// <summary>The practice room (no run, no floor), or the first run's teaching room while it teaches.</summary>
        static bool InThePracticeRoom()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            if (levels == null || (!levels.InRun && !levels.IsInLevel)) return true;
            PracticeTasks tasks = PracticeTasks.Current;
            return tasks != null && tasks.Teaching;
        }

        /// <summary>
        /// The cone at the cursor: its degrees, her distance to the cursor on the ground, its real width there in metres and on the
        /// screen, the cursor's place and the aim's way on the screen; the cone's edges on the ground and her feet (the practice room's).
        /// </summary>
        public static ConeAtTheCursor Measure(PlayerCharacter p, Camera cam)
        {
            var c = new ConeAtTheCursor { Shown = p != null && cam != null };
            if (!c.Shown) return c;
            float height = InterfaceStyle.ScreenHeight;
            Vector3 sight = p.CursorOnItsRay, her = p.Position;
            Vector3 flat = new Vector3(sight.x - her.x, 0f, sight.z - her.z);
            float d = flat.magnitude;
            Vector3 along = d > 1e-4f ? flat / d : Vector3.forward;
            Vector3 across = new Vector3(along.z, 0f, -along.x); // to the right of the aim, seen from above
            c.Degrees = p.PrecisionCone;
            c.Distance = d;
            c.Metres = PrecisionRules.WidthAt(c.Degrees, d);
            c.Share = p.PrecisionShare;
            c.AccurateShare = p.Modifiers != null ? p.Modifiers.Stats.PrecisionAccurateShare : PrecisionValues.Live.AccurateShare;
            Vector3 s = cam.WorldToScreenPoint(sight);
            if (s.z <= 0f) return new ConeAtTheCursor();
            c.SightX = s.x;
            c.SightY = height - s.y;
            float half = c.Metres * 0.5f;
            Vector3 a = cam.WorldToScreenPoint(sight + across * half), b = cam.WorldToScreenPoint(sight - across * half);
            c.Pixels = new Vector2(a.x - b.x, a.y - b.y).magnitude;
            // The way across on the screen, from a metre either side (so it is known with no width too); along it, perpendicular.
            Vector3 ua = cam.WorldToScreenPoint(sight + across), ub = cam.WorldToScreenPoint(sight - across);
            Vector2 acr = new Vector2(ua.x - ub.x, -(ua.y - ub.y));
            acr = acr.sqrMagnitude > 1e-8f ? acr.normalized : new Vector2(1f, 0f);
            c.AcrossX = acr.x;
            c.AcrossY = acr.y;
            c.AlongX = acr.y; // a quarter turn of across: where the aim points on the screen (y down)
            c.AlongY = -acr.x;
            Vector3 feet = cam.WorldToScreenPoint(new Vector3(her.x, 0f, her.z));
            Vector3 groundAt = new Vector3(sight.x, 0f, sight.z);
            Vector3 l = cam.WorldToScreenPoint(groundAt - across * half), r = cam.WorldToScreenPoint(groundAt + across * half);
            c.FeetX = feet.x;
            c.FeetY = height - feet.y;
            c.LeftX = l.x;
            c.LeftY = height - l.y;
            c.RightX = r.x;
            c.RightY = height - r.y;
            return c;
        }

        void Play(ref AudioClip clip, SynthRecipe recipe, string name, float volume)
        {
            if (volume <= 0f) return;
            if (clip == null)
            {
                float[] samples = Synth.Render(recipe, Synth.SampleRate);
                clip = AudioClip.Create(name, Mathf.Max(1, samples.Length), 1, Synth.SampleRate, false);
                clip.SetData(samples, 0);
            }
            if (_voice == null)
            {
                _voice = gameObject.AddComponent<AudioSource>();
                _voice.playOnAwake = false;
                _voice.spatialBlend = 0f; // plain 2D, as every sound of the game (the camera is 28 m up)
                _voice.loop = false;
            }
            _voice.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ---------------- The drawing ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Cone.Shown) return;
            GUI.depth = InterfaceDepth.Words; // with the words and signs at her figure: under the skill bar, the HUD and its screens
            Color before = GUI.color;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float s = InterfaceStyle.ScreenHeight / LayoutHeight;
            ConeAtTheCursor c = Cone;
            if (PracticeExtras)
            {
                DrawGroundCone(c, s);
                DrawSpeedBar(c, s);
            }
            DrawBrackets(c, s);
            GUI.matrix = matrixBefore;
            GUI.color = before;
        }

        /// <summary>
        /// The reticle until work order 144's (his pick, sheet 2): two plain bars either side of the cursor, along the aim, their inner
        /// edges <see cref="ConeAtTheCursor.Pixels"/> apart (the cone's real width there); each dark outline only on its outer side.
        /// </summary>
        void DrawBrackets(ConeAtTheCursor c, float s)
        {
            PrecisionSightValues v = Values;
            InterfaceValues iv = InterfaceStyle.V;
            float w = v.BracketWidth * s, len = v.BracketLength * s, dark = v.DarkMore * s;
            float angle = Mathf.Atan2(c.AlongX, -c.AlongY) * Mathf.Rad2Deg; // a bar's long side, up on the screen, turned to the aim
            var sight = new Vector2(c.SightX, c.SightY);
            var across = new Vector2(c.AcrossX, c.AcrossY);
            Color ink = new Color(0f, 0f, 0f, v.DarkAlpha * v.Alpha), light = InterfaceStyle.C(iv.White, v.Alpha);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 middle = sight + across * (side * (c.Pixels * 0.5f + w * 0.5f));
                Bar(middle + across * (side * dark * 0.5f), angle, w + dark, len + dark, ink);
                Bar(middle, angle, w, len, light);
            }
        }

        void DrawGroundCone(ConeAtTheCursor c, float s)
        {
            if (c.Degrees <= 0f) return;
            PrecisionSightValues v = Values;
            Color col = InterfaceStyle.C(InterfaceStyle.V.White, v.GroundAlpha);
            var feet = new Vector2(c.FeetX, c.FeetY);
            Line(feet, new Vector2(c.LeftX, c.LeftY), v.GroundLineWidth * s, col);
            Line(feet, new Vector2(c.RightX, c.RightY), v.GroundLineWidth * s, col);
        }

        void DrawSpeedBar(ConeAtTheCursor c, float s)
        {
            PrecisionSightValues v = Values;
            InterfaceValues iv = InterfaceStyle.V;
            float w = v.BarWidth * s, h = v.BarHeight * s;
            float x = c.FeetX - w * 0.5f, y = c.FeetY + v.BarBelow * s;
            float most = Mathf.Max(0.1f, v.BarMostShare);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, InterfaceStyle.C(iv.Back), 0f, 0f);
            float fill = Mathf.Clamp01(c.Share / most) * w;
            if (fill > 0f)
                GUI.DrawTexture(new Rect(x, y, fill, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                                c.Share <= c.AccurateShare ? InterfaceStyle.C(iv.Teal) : InterfaceStyle.C(iv.White, 0.85f), 0f, 0f);
            float mark = x + Mathf.Clamp01(c.AccurateShare / most) * w;
            GUI.DrawTexture(new Rect(mark - 0.5f * s, y - 2f * s, 1f * s, h + 4f * s), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, InterfaceStyle.C(iv.Gold), 0f, 0f);
        }

        /// <summary>A rounded bar centred on <paramref name="middle"/>, its long side turned <paramref name="angle"/> degrees clockwise from up.</summary>
        static void Bar(Vector2 middle, float angle, float width, float length, Color col)
        {
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, middle);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(middle.x - width * 0.5f, middle.y - length * 0.5f, width, length), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, width * 0.5f);
            GUI.matrix = m;
        }

        /// <summary>A thin line from <paramref name="a"/> to <paramref name="b"/> on the screen.</summary>
        static void Line(Vector2 a, Vector2 b, float width, Color col)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1f) return;
            Bar((a + b) * 0.5f, Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg, width, length, col);
        }
    }
}
