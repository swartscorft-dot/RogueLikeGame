using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Hold to fire for the input driver (work order 132; Kristian ticked "Ja, hold = skyt"; the rules are
    /// <see cref="HoldToFireRules"/>, the switch <see cref="HoldToFireValues.On"/>). Asked once a frame, after the move keys,
    /// the cover key and the skills have reached her and before her own Update: does the held shoot button ask for a basic shot
    /// now? Nothing is allocated.
    /// </summary>
    public static class HoldToFireInput
    {
        /// <summary>
        /// True when the button is held (with no press this frame: a press is a click as before), not waiting to be seen up, and
        /// a click now would start a shot in this frame and is not refused, and no skill on the same button holds or waits for
        /// its recast on it.
        /// </summary>
        public static bool FiresHeld(PlayerCharacter her, InputBindings bindings, bool held, bool waitsForUp)
        {
            if (!HoldToFireValues.On || !held || waitsForUp || her == null) return false; // the usual frame: nothing more is asked
            return HoldToFireRules.Fires(HoldToFireValues.On, held, waitsForUp, her.ClickGoesThisFrame,
                                         her.IsSprinting || her.IsJumping, ButtonTakenBySkill(her, bindings));
        }

        /// <summary>
        /// Does a skill bound to the shoot button itself hold on it now or wait for its recast on it? With the start bindings none
        /// is (the left button shoots, the skills sit on Q 1 2 3 R 4 F and the right button); a rebinding could put one there.
        /// </summary>
        public static bool ButtonTakenBySkill(PlayerCharacter her, InputBindings bindings)
        {
            if (her == null || bindings == null || bindings.Skills == null) return false;
            InputBinding shoot = bindings.Shoot;
            for (int key = 0; key < bindings.Skills.Length && key < 32; key++)
            {
                if (!Same(bindings.Skills[key], shoot)) continue;
                int slot = her.SkillOnKey(key);
                if (slot < 0) continue;
                if (her.ActiveSkillSlot == slot || her.RecastWindowOpenFor(slot)) return true;
            }
            return false;
        }

        static bool Same(InputBinding a, InputBinding b) => a.Source == b.Source && (a.Source != BindingSource.Key || a.Key == b.Key);
    }
}
