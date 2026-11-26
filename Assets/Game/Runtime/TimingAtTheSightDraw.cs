using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The drawing of the cues at the sight (work order 134): the cylinder and the ring, and the cue's name after the temporary
    /// key, in the interface's palette, round the sight's point. Every line and circle is the engine's own rounded rectangle
    /// (<see cref="GUI.DrawTexture(Rect, Texture, ScaleMode, bool, float, Color, float, float)"/> with a border's width and
    /// radius): drawn smooth at its edges at any size on the screen, so a thin line cannot break into specks as a line laid in
    /// the world can at our camera. Each light line lies on a dark one 2 px wider, so it reads on a lit floor and against the
    /// effects. Nothing filled in the middle: the cylinder's inside and the band's are open, the enemy under the cursor seen.
    /// </summary>
    public sealed partial class TimingAtTheSight
    {
        const float DarkMore = 3f;      // px the dark line under a light one is wider by (2 in the first pictures: lost on a white dummy)
        const float DarkAlpha = 0.75f;  // its opacity
        const float LayoutHeight = 1080f; // px: the sizes are given for a screen this high

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.depth = InterfaceDepth.Words; // with the words and signs at her figure: under the skill bar, the HUD and its screens
            bool name = NameShowing && TimingAtTheSightValues.On && !PlayerSigns.ScreenUp;
            if (!(Drawn && Cue != TimingCue.Hammer) && !name) return;
            Color before = GUI.color;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float s = InterfaceStyle.ScreenHeight / LayoutHeight;
            if (Drawn && Cue == TimingCue.Cylinder) DrawCylinder(s);
            else if (Drawn && Cue == TimingCue.Ring) DrawRing(s);
            if (name) DrawName(s);
            GUI.matrix = matrixBefore;
            GUI.color = before;
        }

        // ---------------- The pieces ----------------

        /// <summary>A circle's line round <paramref name="c"/>, its middle at <paramref name="r"/>, <paramref name="w"/> wide.</summary>
        static void Circle(Vector2 c, float r, float w, Color col)
        {
            float outer = r + w * 0.5f;
            if (outer <= 0f || w <= 0f) return;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(c.x - outer, c.y - outer, outer * 2f, outer * 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, Mathf.Min(w, outer), outer);
        }

        /// <summary>A circle's line on a dark one a little wider.</summary>
        static void InkedCircle(Vector2 c, float r, float w, Color col, float s)
        {
            Circle(c, r, w + DarkMore * s, new Color(0f, 0f, 0f, DarkAlpha * col.a));
            Circle(c, r, w, col);
        }

        /// <summary>A filled disc.</summary>
        static void Disc(Vector2 c, float r, Color col)
        {
            if (r <= 0f) return;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, r);
        }

        /// <summary>A pill standing out from <paramref name="c"/> at <paramref name="angle"/> (degrees clockwise from the top), its middle <paramref name="r"/> out.</summary>
        static void Post(Vector2 c, float angle, float r, float length, float width, Color col)
        {
            Matrix4x4 m = GUI.matrix;
            Vector2 at = c + Toward(angle) * r;
            GUIUtility.RotateAroundPivot(angle, at);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(at.x - width * 0.5f, at.y - length * 0.5f, width, length), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, width * 0.5f);
            GUI.matrix = m;
        }

        /// <summary>The way out from the middle at this angle, on the screen (y down).</summary>
        static Vector2 Toward(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
        }

        static Color Ink(Rgba c, float alpha) => InterfaceStyle.C(c, alpha);

        // ---------------- The cylinder ----------------

        void DrawCylinder(float s)
        {
            TimingAtTheSightValues v = V;
            InterfaceValues iv = InterfaceStyle.V;
            float a = Alpha, real = Time.time;
            Vector2 c = Sight;
            float kick = real - _flashAt < v.LockSeconds ? v.LockKick * (1f - (real - _flashAt) / Mathf.Max(1e-3f, v.LockSeconds)) : 0f;
            float sink = !_live && _endMissed && v.LingerSeconds > 0f ? v.MissDrop * Mathf.Clamp01((real - _endedAt) / v.LingerSeconds) : 0f;
            float r = (v.CylinderRadius + kick) * (1f - sink) * s, cr = v.ChamberRadius * s;
            bool lit = Lit && CylinderSays;
            float flash = v.FlashSeconds > 0f && real - _flashAt < v.FlashSeconds ? 1f - (real - _flashAt) / v.FlashSeconds : 0f;

            // The cylinder's ring.
            InkedCircle(c, r, v.CylinderStroke * s, Ink(iv.White, 0.8f * a), s);

            // The notch: two posts at the top, a chamber's middle between them exactly when it is in the notch.
            float halfChamber = Mathf.Asin(Mathf.Clamp01(v.ChamberRadius / Mathf.Max(1e-3f, v.CylinderRadius))) * Mathf.Rad2Deg;
            float postAngle = Notch * 0.5f + halfChamber + Mathf.Atan2(v.PostWidth * 0.5f, v.CylinderRadius) * Mathf.Rad2Deg;
            float pr = r + cr + (v.PostGap + v.PostLength * 0.5f) * s;
            Color post = lit || flash > 0f ? Ink(iv.Teal, a) : Ink(iv.White, a);
            for (int side = -1; side <= 1; side += 2)
            {
                Post(c, side * postAngle, pr, (v.PostLength + DarkMore) * s, (v.PostWidth + DarkMore) * s, new Color(0f, 0f, 0f, DarkAlpha * a));
                Post(c, side * postAngle, pr, v.PostLength * s, v.PostWidth * s, post);
            }

            // The chambers: the first shot's fired, the pressed ones as they fell, the one waited for bright, the later ones dark.
            for (int i = 0; i < Chambers; i++)
            {
                Vector2 at = c + Toward(CylinderCue.AngleOf(i, Turn, Spacing)) * r;
                DrawChamber(at, cr, ChamberState(i), i == _flashChamber ? flash : 0f, lit && i == LiveChamber, a, s, iv);
            }
        }

        enum Chamber { Coming, Waited, Fired, Clean, Dull, Missed }

        Chamber ChamberState(int chamber)
        {
            int press = _openingMove ? chamber + 2 : chamber + 1;
            if (press <= 1) return Chamber.Fired; // the chain's first shot
            if (_live && LiveChamber == chamber) return Chamber.Waited;
            RecastTiming t = _player != null ? _player.RecastTimingOf(press) : RecastTiming.None;
            switch (t)
            {
                case RecastTiming.Clean: return Chamber.Clean;
                case RecastTiming.Missed: return Chamber.Missed;
                case RecastTiming.None: return !_live && _endMissed && press == _endPress ? Chamber.Missed : Chamber.Coming;
                default: return Chamber.Dull;
            }
        }

        static void DrawChamber(Vector2 at, float r, Chamber state, float flash, bool lit, float a, float s, InterfaceValues iv)
        {
            float rim = 2.5f * s;
            Color dark = new Color(0f, 0f, 0f, 0.8f * a);
            Disc(at, r + DarkMore * 0.5f * s + (state == Chamber.Clean ? 2f * flash * s : 0f), state == Chamber.Clean && flash > 0f ? Ink(iv.White, a) : dark);
            switch (state)
            {
                case Chamber.Waited: // the one waited for: a bright white rim; in the notch and clean, filled teal
                    if (lit) Disc(at, r - 0.5f * s, Ink(iv.Teal, a));
                    Circle(at, r - rim * 0.6f, rim * 1.2f, Ink(iv.White, a));
                    break;
                case Chamber.Clean: // pressed clean: filled teal
                    Disc(at, r - 0.5f * s, Ink(iv.Teal, a));
                    break;
                case Chamber.Dull: // pressed, not clean: filled grey
                    Disc(at, r - rim * 0.5f, Ink(iv.Grey, 0.85f * a));
                    break;
                case Chamber.Missed: // its window ran out: a red rim
                    Circle(at, r - rim * 0.5f, rim, Ink(iv.Red, a));
                    break;
                case Chamber.Fired: // the chain's first shot: a spent case
                    Circle(at, r - rim * 0.5f, rim * 0.8f, Ink(iv.Grey, 0.7f * a));
                    Disc(at, r * 0.35f, Ink(iv.Grey, 0.8f * a));
                    break;
                default: // coming: dark, a dim rim
                    Circle(at, r - rim * 0.5f, rim * 0.8f, Ink(iv.Grey, 0.75f * a));
                    break;
            }
        }

        // ---------------- The ring ----------------

        void DrawRing(float s)
        {
            TimingAtTheSightValues v = V;
            InterfaceValues iv = InterfaceStyle.V;
            float a = Alpha, real = Time.time;
            Vector2 c = Sight;
            bool lit = Lit && RingSays;
            float gone = v.FlashSeconds > 0f && real - _goneAt < v.FlashSeconds * 2f ? 1f - (real - _goneAt) / (v.FlashSeconds * 2f) : 0f;
            bool flash = gone > 0f && _goneClean;

            // The band: a faint fill between two lines; lit, brighter.
            float inner = v.BandInner * s, outer = v.BandOuter * s;
            Circle(c, (inner + outer) * 0.5f, outer - inner, lit || flash ? Ink(iv.Teal, 0.22f * a) : Ink(iv.Teal, 0.1f * a));
            Color edge = lit || flash ? Ink(iv.White, a) : Ink(iv.Teal, 0.85f * a);
            InkedCircle(c, outer, 2f * s, edge, s);
            InkedCircle(c, inner, 2f * s, edge, s);

            // The ring pressed, where it was: lit if clean, grey if not, fading.
            if (gone > 0f)
                InkedCircle(c, _goneRadius * s, v.RingStroke * s, _goneClean ? Ink(iv.Teal, gone * a) : Ink(iv.Grey, gone * a), s);

            // The ring closing in, while a press waits (a miss: on in to its least, red, fading).
            float shows;
            float radius;
            Color ink;
            if (_live && Clean.Any)
            {
                radius = RingRadius;
                shows = RingCue.Shows(radius, v);
                ink = lit ? Ink(iv.Teal, shows * a) : Ink(iv.White, shows * a);
            }
            else if (!_live && _endMissed)
            {
                radius = v.RingLeast;
                shows = 1f;
                ink = Ink(iv.Red, a);
            }
            else return;
            if (shows > 0f) InkedCircle(c, radius * s, v.RingStroke * s, ink, s);
        }

        // ---------------- The name after the temporary key ----------------

        void DrawName(float s)
        {
            TimingAtTheSightValues v = V;
            InterfaceValues iv = InterfaceStyle.V;
            float age = Time.unscaledTime - _nameAt;
            float a = Mathf.Clamp01((v.NameSeconds - age) / Mathf.Max(1e-3f, v.NameSeconds * 0.25f));
            GUIStyle style = InterfaceStyle.Text(true, Mathf.Max(8, Mathf.RoundToInt(v.NameSize * s)), TextAnchor.MiddleCenter);
            float y = Sight.y + v.NameBelow * s;
            var rect = new Rect(Sight.x - 300f * s, y - 20f * s, 600f * s, 40f * s);
            InterfaceStyle.Label(rect, _nameContent, style, Ink(iv.White, a), true);
        }
    }
}
