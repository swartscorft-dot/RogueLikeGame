namespace Roguelike.Rules
{
    /// <summary>How the anti-aliasing smooths the picture's edges (work order 125).</summary>
    public enum AntiAliasingKind
    {
        /// <summary>URP's multisampling, 4 samples a pixel: the edges of every model smoothed where they are drawn; textures and glows untouched.</summary>
        Msaa4x,
        /// <summary>URP's SMAA (high), a pass over the finished picture that finds hard edges and softens them.</summary>
        Smaa,
    }

    /// <summary>
    /// The anti-aliasing of the whole picture (work order 125). Kristian, 2026-11-25, of work order 119's pictures: "man ser mange
    /// fragmenterte pixler rundt omkring, det ser veldig dårlig ut"; the game drew with none at all (render scale 1, no MSAA, no
    /// post anti-aliasing), so every edge was stair-stepped. On by default; the pause menu's line "Anti-aliasing" flips it while
    /// he plays, so that he can compare. Off, the camera draws exactly as before work order 125. Not kept between starts.
    /// </summary>
    public static class AntiAliasingValues
    {
        /// <summary>The switch: on, the game's camera smooths its edges by <see cref="Kind"/>; off, as before (none). On by default.</summary>
        public static bool On = true;

        /// <summary>
        /// Which kind (the producer's choice after comparing both in pictures, see work order 125's report): multisampling keeps
        /// the textures, the thin teal glows and the line's own soft edge as sharp as they are and smooths only the models'
        /// edges; SMAA also softens edges inside textures and the thin glows a little.
        /// </summary>
        public static AntiAliasingKind Kind = AntiAliasingKind.Msaa4x;

        /// <summary>Samples a pixel when <see cref="Kind"/> is <see cref="AntiAliasingKind.Msaa4x"/> (the URP asset's own MSAA is set to this; the switch off draws with 1).</summary>
        public const int MsaaSamples = 4;

        /// <summary>True while the camera is to multisample.</summary>
        public static bool MsaaNow => On && Kind == AntiAliasingKind.Msaa4x;
        /// <summary>True while the camera is to run SMAA.</summary>
        public static bool SmaaNow => On && Kind == AntiAliasingKind.Smaa;
    }
}
