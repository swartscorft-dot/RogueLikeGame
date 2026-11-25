using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Which renderers the clean ink line is drawn round (work order 125; Kristian, 2026-11-25, of work order 119's pictures: "man
    /// ser mange fragmenterte pixler rundt omkring, det ser veldig dårlig ut"): her model's renderers (marked by
    /// <see cref="ToonLook"/>) and each enemy's own (by its <see cref="EnemyToonLook"/>), and nothing else. The mark is a bit of
    /// the renderer's own shader user value (<see cref="Renderer.SetShaderUserValue"/>), which the toon shader's line reads: not a
    /// material, a shader pass or a layer, so a thing that copies an enemy's materials and its rendering layers (a loose bone
    /// piece, work order 122) is not marked and draws its two tones with no line. While the clean line is off
    /// (<see cref="Rules.ToonLookValues.CleanInk"/> false) the shader does not ask for the mark (work order 119's line, exactly).
    /// Nothing else in the game uses a renderer's user value; the other bits are kept as they are.
    /// </summary>
    public static class ToonInkFigures
    {
        /// <summary>The bit of a renderer's shader user value that marks it (the toon shader's <c>INK_MARK</c>).</summary>
        public const uint InkBit = 1u;

        /// <summary>Marks <paramref name="r"/> (a mesh or skinned mesh renderer) for the line, or takes the mark off.</summary>
        public static void Mark(Renderer r, bool on)
        {
            uint was = UserValue(r);
            uint now = on ? was | InkBit : was & ~InkBit;
            if (now == was) return;
            if (r is SkinnedMeshRenderer s) s.SetShaderUserValue(now);
            else if (r is MeshRenderer m) m.SetShaderUserValue(now);
        }

        /// <summary>True when <paramref name="r"/> is marked for the line.</summary>
        public static bool Has(Renderer r) => (UserValue(r) & InkBit) != 0;

        /// <summary>The renderer's shader user value (0 for any other kind of renderer, or none).</summary>
        public static uint UserValue(Renderer r) =>
            r == null ? 0u : r is SkinnedMeshRenderer s ? s.GetShaderUserValue() : r is MeshRenderer m ? m.GetShaderUserValue() : 0u;
    }
}
