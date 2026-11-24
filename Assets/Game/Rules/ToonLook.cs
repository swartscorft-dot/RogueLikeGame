using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How her model is shaded in the toon look's trial (work order 115), as numbers. Kristian, 2026-11-24, looking at the key art
    /// of her drawn for the character selection (runner\codex\images\menu\char_bg_a_kinetic.png and char_bg_b_crypt.png): "Jeg
    /// liker også denne tegnestilen veldig godt. Klarer vi å få modellen i spillet til å ha mer av det?" The drawings' look as the
    /// producer read it: a bold dark ink line round her and along the big inner edges, flat fields of colour in two tones with a
    /// hard edge between them, deep slightly coloured blacks, a cyan-teal light along her edge, her pistols' teal lights glowing,
    /// no small noisy detail. Only HER model wears it (her body and the weapons on her); nothing draws her through other things
    /// (he ticked "La det være" to an outline seen through enemies, 2026-11-24). Every value is a first guess he tunes by looking.
    /// Colours are as picked (sRGB), the way a material's colour is set.
    /// </summary>
    [Serializable]
    public sealed class ToonLookValues
    {
        /// <summary>
        /// The order's switch: on, her model wears the toon shader; off, her materials are exactly today's (the same material
        /// objects, nothing in them changed). On by default (work order 115, a trial); the pause menu's line "Toon look (trial)"
        /// flips it while he plays, and the game follows it the same frame. Not kept between starts: every Play starts on.
        /// </summary>
        public static bool On = true;

        /// <summary>The values the game draws her with (one set, read when the look is put on her).</summary>
        public static readonly ToonLookValues Live = new ToonLookValues();

        // Two tones (the order's point 1).
        public float LitThreshold = 0.05f;   // where light turns to shadow, in N·L (the cosine between her surface and the light): a little past side-on, so a bit under half of her is in shadow as in the drawings
        public float LitSoftness = 0.03f;    // the hard edge's half width in N·L: small, a clean edge that does not crawl into steps
        public float LitLevel = 0.9f;        // the lit tone as a share of a light's full strength (today's Lit gives about 0.6 of it on the average lit face; flat at 0.9 the lit side reads about as bright, not brighter)
        public Rgb ShadowTint = new Rgb(0.26f, 0.23f, 0.36f); // the shadow tone as a share of a light: dark and a little cool and violet, a coloured black and not grey (the drawings' shadows)
        public float ShadowEdge = 0.5f;      // a cast shadow (a wall, a pillar) counts as shadow where the light's shadow factor is under this, with the same soft edge

        // The room's light without a direction (the scene's ambient, set by the lamplit view).
        public float AmbientFlat = 1f;       // 1: the ambient is the same on every face (no gradient up to down), 0: as today's
        public float AmbientLevel = 1f;      // share of the ambient she takes

        // Flatter colour (the order's point 4): her baked texture is calm already (large painted fields from the model maker,
        // with fine scratches, stitching and baked shading in them); her normal map carries the bumpy detail, and it is not used.
        public float DetailCalm = 1.5f;      // mip levels her texture is read blurrier by (each halves its detail): 1.5 takes out what is under about 3 texels of her 2048 texture, keeps the fields

        // The edge light (the order's point 3).
        public Rgb RimColour = new Rgb(0.30f, 0.95f, 0.86f); // the drawings' cyan-teal
        public float RimStrength = 0.4f;     // its light at its brightest (in linear light, under the bloom's threshold of 1.2: it never blooms)
        public float RimStart = 0.75f;       // where it begins, in 1 - N·V (0 facing the camera, 1 side-on to it): only the outer edge of her (0.62 at first lit whole hat brims and shields seen edge-on in the first pictures)
        public float RimSoftness = 0.05f;    // its edge's half width in the same measure
        public float RimAway = 0.85f;        // how much it keeps to the side of her away from the key light: 0 all round, 1 only there
        public float RimUnder = 0.5f;        // it is gone on faces turned down by this much (-N·y) and fades in to side-on: the first pictures lit the whole underside of her hat's brim teal

        // Her pistols' teal lights (the order's point 3): read out of their texture by colour, glowing as the game's other glows.
        public float KitTealGlow = 1.05f;    // the teal parts' light on her weapons, at the full mask (just under the bloom's threshold of 1.2, as the other small glows are)
        public float BodyTealGlow = 0f;      // and on her body (its texture has a little teal in the eyes and some marks: left unlit, the producer's choice)
        public float TealStart = 0.10f;      // a texel is teal when its green and blue both stand over its red by more than this (0 to 1)
        public float TealSoftness = 0.15f;   // and fully so this much more over

        // The ink line (the order's point 2): an inverted hull, her mesh drawn a second time inside out and pushed out along
        // its own normals in screen space, so the line has the same width in pixels at every camera distance.
        public float InkWidth = 2.25f;       // px at 1080 rows of screen (the producer's start: 2 to 2.5); in proportion at other sizes
        public Rgb InkColour = new Rgb(0.06f, 0.04f, 0.06f); // a deep warm black, the drawings' line (not pure black)
        public float InkDepthPush = 0.06f;   // m the line is pushed away from the camera: a small piece lying on her (a buckle, a pouch) is not inked against the coat under it, only edges that stand off by more (the first pictures had scratchy inner lines and specks at the game's distance)

        /// <summary>The ink line's width in pixels on a screen <paramref name="screenRows"/> pixels tall: in proportion to 1080 rows.</summary>
        public float InkPixels(float screenRows) => InkWidth * Math.Max(0f, screenRows) / ReferenceRows;

        /// <summary>The screen height the ink width is named at.</summary>
        public const float ReferenceRows = 1080f;
    }
}
