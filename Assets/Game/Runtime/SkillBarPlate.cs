using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Her health and focus at the top left in the concept's style (work order 068, his choice of the evening of 2026-11-21:
    /// "Begge: plate oppe, små under"), as part of <see cref="SkillBar"/>: from the screen's corner the concept's dark wedge
    /// with its teal edge, where a portrait can stand later (none is drawn); her health as a slanted red plate with a heavy
    /// black outline, halftone dots fading toward its right end and the number ("72 / 100") in the heavy white italic, the
    /// part of it that is gone drawn back along the slant over the dark plate, white for a moment where it was just lost;
    /// under it the word FOCUS and a slanted teal bar that fills (Hair Trigger's in parts), and when the gauge is full it
    /// turns white-hot with a pulsing teal edge and its key on a plate at its end. A hit she takes flashes the plate white
    /// and shakes it. The small strokes at her feet stay (SkillBarKinetic.cs), smaller.
    /// What it shows is worked out in <see cref="Tick"/>; its texts are made when what they say changes, never in a frame.
    /// </summary>
    public sealed partial class SkillBar
    {
        /// <summary>The top plate is drawn now (the skin and <see cref="InterfaceValues.TopPlate"/> on, she is there).</summary>
        public bool PlateShown { get; private set; }
        /// <summary>The red plate, the focus bar and everything the plate covers this frame (layout pixels), before any shake.</summary>
        public Rect PlateHealthRect { get; private set; }
        public Rect PlateFocusRect { get; private set; }
        public Rect PlateWhole { get; private set; }
        /// <summary>The number on the red plate: her health over its most ("72 / 100").</summary>
        public string PlateNumber { get; private set; } = "";
        /// <summary>A hit's flash on the plate and the strokes now, 1 to 0.</summary>
        public float HitFlash { get; private set; }
        /// <summary>The gauge is full (or her focus is spent into a window): the bar is white-hot and its key shows.</summary>
        public bool PlateFocusFull => _focusBright;

        int _plateCur = int.MinValue, _plateMax = int.MinValue;
        float _hitAt = float.NegativeInfinity, _healthWas = -1f;

        /// <summary>The plate's part of the frame's work (from <see cref="Tick"/>).</summary>
        void TickPlate(InterfaceValues v)
        {
            PlateShown = v.TopPlate;
            PlateHealthRect = R(InterfacePlate.Health(v));
            PlateFocusRect = R(InterfacePlate.Focus(v));
            PlateWhole = R(InterfacePlate.Whole(v));
            int cur = Health != null ? Mathf.Max(0, Health.Current) : 0, max = Health != null ? Mathf.Max(0, Health.Max) : 0;
            if (cur != _plateCur || max != _plateMax)
            {
                _plateCur = cur;
                _plateMax = max;
                PlateNumber = InterfaceStyle.Number(cur) + " / " + InterfaceStyle.Number(max); // made when her health changes, not in a frame
            }
            float now = Time.unscaledTime;
            if (_healthWas >= 0f && HealthShare < _healthWas - 1e-5f) _hitAt = now;
            _healthWas = HealthShare;
            float since = now - _hitAt;
            HitFlash = v.HitFlashSeconds > 0f && since >= 0f && since < v.HitFlashSeconds ? 1f - since / v.HitFlashSeconds : 0f;
        }

        void DrawTopPlate(InterfaceValues v)
        {
            float s = v.PlateSlant, o = v.HeavyOutline, d = v.Depth;
            float shake = HitFlash > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * v.HitShake * HitFlash : 0f;
            Matrix4x4 before = GUI.matrix;
            if (shake != 0f) GUI.matrix = before * Matrix4x4.Translate(new Vector3(shake, shake * 0.35f, 0f));

            // The corner: the dark wedge where a portrait would stand, its leaning edge teal, a teal cut along its foot.
            Rect c = R(InterfacePlate.Corner(v));
            float cp = c.center.y;
            InterfaceStyle.Leaning(c, cp, s, InterfaceStyle.C(v.Black, 0.9f));
            InterfaceStyle.Leaning(new Rect(c.xMax - 7f, c.y, 7f, c.height), cp, s, InterfaceStyle.C(v.Teal));
            InterfaceStyle.Leaning(new Rect(c.x, c.yMax - 9f, c.width - 18f, 5f), cp, s, InterfaceStyle.C(v.Teal, 0.9f));
            InterfaceStyle.DotsAlong(new Rect(c.x + c.width * 0.3f, c.y, c.width * 0.62f, c.height), cp, s, 0.25f, InterfaceStyle.C(v.Teal, 0.22f));

            // Her health: the red plate on its black outline, drawing back along the slant over the dark plate.
            Rect h = PlateHealthRect;
            float hp = h.center.y;
            // One black band behind both rows, so the plate reads as one piece off the corner.
            // Its left edge meets the corner's teal edge on the same lean (both lean about their own middles: shifted to meet).
            float top = h.y - 2f, bottom = PlateFocusRect.yMax + 10f;
            float bandX = c.xMax + s * (cp - (top + bottom) * 0.5f);
            var band = new Rect(bandX, top, h.xMax + 26f - bandX, bottom - top);
            InterfaceStyle.Leaning(band, band.center.y, s, InterfaceStyle.C(v.Black, 0.78f));
            InterfaceStyle.HeavyPlate(h, hp, s, o, d);
            InterfaceStyle.Leaning(h, hp, s, InterfaceStyle.C(v.Panel));
            InterfaceStyle.Leaning(new Rect(h.x, h.y, h.width * HealthShare, h.height), hp, s, InterfaceStyle.C(v.Red));
            if (LostAlpha > 0f && LostFrom > HealthShare)
                InterfaceStyle.Leaning(new Rect(h.x + h.width * HealthShare, h.y, h.width * (Mathf.Clamp01(LostFrom) - HealthShare), h.height), hp, s, InterfaceStyle.C(v.White, LostAlpha));
            InterfaceStyle.DotsAlong(h, hp, s, 1f - v.PlateDots, InterfaceStyle.C(v.Black, 0.8f));
            InterfaceStyle.Leaning(new Rect(h.x, h.y, h.width, 3f), hp, s, InterfaceStyle.C(v.White, 0.8f)); // the inner edge
            InterfaceStyle.Slashes(h.xMax + 24f, h.y + h.height * 0.18f, h.height * 0.64f, 6f, 2, InterfaceStyle.C(v.Red));
            if (HitFlash > 0f) InterfaceStyle.Leaning(h, hp, s, InterfaceStyle.C(v.White, 0.75f * HitFlash));
            // The number over the flash (round 2's picture: under it, the hit hid her health at the moment it mattered); black while it flashes.
            float inset = InterfacePlate.HalfLean(h.height, s) + 14f;
            InterfaceStyle.Label(new Rect(h.x + inset, h.y, h.width - inset, h.height), PlateNumber,
                                 InterfaceStyle.Text(true, v.PlateNumberSize, TextAnchor.MiddleLeft),
                                 HitFlash > 0.4f ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White), HitFlash <= 0.4f);

            // Focus: the word, and its bar; full, white-hot with a pulsing teal edge and its key.
            Rect f = PlateFocusRect;
            float fp = f.center.y;
            bool full = _focusBright;
            InterfaceStyle.Label(R(InterfacePlate.FocusWord(v)), "FOCUS", InterfaceStyle.Text(true, v.FocusWordSize, TextAnchor.MiddleRight),
                                 full ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White), true);
            InterfaceStyle.HeavyPlate(f, fp, s, o * 0.75f, d * 0.75f);
            // Round 1's picture: an empty gauge on the black band read as nothing; its slot is a dim teal now, so the bar is there.
            InterfaceStyle.Leaning(f, fp, s, InterfaceStyle.C(v.Teal, 0.16f));
            InterfaceStyle.Leaning(new Rect(f.x, f.yMax - 3f, f.width, 3f), fp, s, InterfaceStyle.C(v.Teal, 0.45f));
            if (Player.FocusMode == FocusMode.HairTrigger) PlateParts(f, fp, s, v);
            else InterfaceStyle.Leaning(new Rect(f.x, f.y, f.width * FocusShare, f.height), fp, s, full ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal));
            if (full)
            {
                float pulse = 1f - v.FullPulse * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * v.FullPulseRate * 2f * Mathf.PI));
                float g = 3f;
                InterfaceStyle.LeaningFrame(new Rect(f.x - o - g, f.y - o - g, f.width + 2f * (o + g), f.height + 2f * (o + g)), fp, s, InterfaceStyle.C(v.Teal, pulse), g);
                if (_focusKey.Length > 0)
                {
                    Rect key = R(InterfacePlate.FocusKey(v));
                    InterfaceStyle.HeavyPlate(key, key.center.y, s, o * 0.75f, 0f);
                    InterfaceStyle.Leaning(key, key.center.y, s, InterfaceStyle.C(v.Teal));
                    InterfaceStyle.Label(key, _focusKey, InterfaceStyle.Text(true, v.FocusWordSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
                }
            }
            else InterfaceStyle.Leaning(new Rect(f.x, f.y, f.width, 2f), fp, s, InterfaceStyle.C(v.White, 0.5f));
            if (_stateWord.Length > 0)
            {
                GUIStyle small = InterfaceStyle.Text(true, v.ComboTagSize, TextAnchor.UpperLeft);
                var r = new Rect(f.x + 6f, f.yMax + o + 3f, f.width, v.ComboTagSize * 1.3f);
                InterfaceStyle.Label(r, _stateWord, small, InterfaceStyle.C(v.Teal), true);
                float w = InterfaceStyle.Measure(_stateWord, small).x;
                InterfaceStyle.Label(new Rect(r.x + w + 6f, r.y, 60f, r.height), _stateNumber, small, InterfaceStyle.C(v.White), true);
            }
            if (HitFlash > 0f) InterfaceStyle.Leaning(f, fp, s, InterfaceStyle.C(v.White, 0.6f * HitFlash));
            GUI.matrix = before;
        }

        /// <summary>Hair Trigger's parts on the plate's bar: each filled by the focus in it, white when full; framed teal while primed.</summary>
        void PlateParts(Rect f, float pivot, float s, InterfaceValues v)
        {
            int parts = Mathf.Max(1, Player.FocusParts);
            float size = FocusGauge.PartSize(Player.Identity, parts);
            float gap = v.PartGap * 2f;
            float w = (f.width - gap * (parts - 1)) / parts;
            for (int p = 0; p < parts; p++)
            {
                float fill = size > 0f ? Mathf.Clamp01((Player.Focus - p * size) / size) : 0f;
                var part = new Rect(f.x + p * (w + gap), f.y, w, f.height);
                if (p > 0) InterfaceStyle.Leaning(new Rect(part.x - gap, f.y, gap, f.height), pivot, s, InterfaceStyle.C(v.Black));
                if (fill > 0f) InterfaceStyle.Leaning(new Rect(part.x, part.y, part.width * fill, part.height), pivot, s, fill >= 1f ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal));
            }
            if (Player.IsPrimed) InterfaceStyle.LeaningFrame(new Rect(f.x - 4f, f.y - 4f, f.width + 8f, f.height + 8f), pivot, s, InterfaceStyle.C(v.Teal), 2f);
        }
    }
}
