using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 151 (the producer's, 2026-11-27, from a finding of work order 149's builder: "under the toon look, a colour laid on an
    /// enemy's material does not show at all; only the way the hit flash is laid on shows"). What was found: the toon look is not the
    /// cause. On a floor every enemy's model is drawn a second time by the rooms' mood (work order 044, <c>RoomMood.FloorEnemy</c>): its
    /// own texture, UNTINTED, added at the floor's light floor for enemies (0.30 to 0.42 of full, the lamplit steps) so that it reads in
    /// the dark. In a dark room that added picture outweighs the lit body (the floor's ambient is 0.07 to 0.2), so a tint laid on the
    /// body's material (the elite's gold, the warder's green, the daze's pale blue, the lancer's red, the Grave Ram's ash white, the
    /// caller's magenta) is drawn only where a lamp lights the body; the hit flash, laid over the whole renderer as a property block of
    /// _BaseColor, reaches the added picture too, and so it shows. In the practice room (no mood, a light floor) every tint shows, in
    /// both looks.
    ///
    /// The repair: the added picture of each enemy mesh whose own colour is not white is given that colour as well, by a property
    /// block on its slot alone (the hit flash's way, one slot), at the floor's own level, so that it is the body's colour exactly as
    /// the body is (its texture times its tint), no stronger. Composed with the flash and the Grave Ram's daze (which lay a block over
    /// the whole renderer): while either is on, nothing of this order's is laid, and their block is today's exactly; as it ends the
    /// tint comes back.
    /// </summary>
    public static class EnemyTintValues
    {
        /// <summary>
        /// The order's switch, ON: a tinted enemy's added picture on a dark floor (the mood's light floor for enemies) carries its tint,
        /// so the elite's gold, the warder's green, the daze, the lancer's red, the bomber's ember, the Grave Ram's ash white and the
        /// caller's colour show in the game's rooms as their orders chose them. It repairs what work order 149's builder found
        /// ("a colour laid on an enemy's material does not show"). Off: no property block of this order's is ever laid, every
        /// enemy's renderers are drawn exactly as before (a test proves the blocks are today's).
        /// </summary>
        public static bool Show = true;

        /// <summary>
        /// How far from white (in each of red, green and blue, as picked) a mesh's own colour may be and still count as untinted: one
        /// step of an 8-bit colour, so a colour set to white is white and every named tint of the game (the nearest, the Grave Ram's
        /// ash white, is 0.14 below) is a tint.
        /// </summary>
        public const float WhiteWithin = 1f / 255f;
    }

    /// <summary>The rules of work order 151's tint on an enemy's added picture (<see cref="EnemyTintValues"/>), without the engine.</summary>
    public static class EnemyTintRules
    {
        /// <summary>True when a mesh's own colour (as picked) is a tint: not white within <see cref="EnemyTintValues.WhiteWithin"/>.</summary>
        public static bool Tinted(Rgb own) =>
            Math.Abs(own.R - 1f) > EnemyTintValues.WhiteWithin || Math.Abs(own.G - 1f) > EnemyTintValues.WhiteWithin || Math.Abs(own.B - 1f) > EnemyTintValues.WhiteWithin;

        /// <summary>
        /// Is this order's block laid on a mesh's added picture now? With the switch on, the mesh tinted, its added picture drawn from its
        /// texture (the mood's for a model; a capsule's added picture is made in its own colour already, and is left as it is), and
        /// neither a hit's flash nor another block over the whole renderer (the Grave Ram's daze) on: those are today's, and win.
        /// </summary>
        public static bool Lays(bool show, bool tinted, bool textured, bool flashing, bool ownBlockOver) =>
            show && tinted && textured && !flashing && !ownBlockOver;

        /// <summary>
        /// The colour the added picture is drawn with, in linear light (what the shader reads): the floor's level for it, as the mood set
        /// it (its colour in linear light), times the mesh's own tint in linear light; so the added picture is the texture times the tint
        /// times the level, as the lit body is the texture times the tint times its light.
        /// </summary>
        public static Rgb Added(Rgb levelLinear, Rgb tintLinear) =>
            new Rgb(levelLinear.R * tintLinear.R, levelLinear.G * tintLinear.G, levelLinear.B * tintLinear.B);
    }
    /// <summary>
    /// Work order 151's line on the settings screen's ROOMS page (the producer's ruling at the merge of 2026-11-28, W4: he compares by
    /// playing): "Enemy tints", "Show" (the default, nothing stored: <see cref="EnemyTintValues.Show"/>'s own) or "As before" (no block of
    /// the order's laid, every enemy drawn as before), after "Timed chest". The game keeps it in EnemyTintSettings.
    /// </summary>
    public static class EnemyTintLines
    {
        public const string Name = "Enemy tints";
        public const string ShowWords = "Show", BeforeWords = "As before";

        /// <summary>The line's words.</summary>
        public static string Words(bool show) => show ? ShowWords : BeforeWords;
    }
}
