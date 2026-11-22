using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Light from the lamps on the floors (work order 085, <see cref="LamplitView"/>), as part of <see cref="GeneratedLevels"/>:
    /// the floor she is on follows the setting (<see cref="GameSettings.LightStep"/>: the pause menu's "Light" line), looked at
    /// once a frame with the greybox view (after it), so the light changes in the middle of a run without the floor being built
    /// again. The greybox view wins: with it on, the floor has the greybox's even light and no lamplit light. With "as before"
    /// on a floor where no lamplit level was chosen, nothing is added at all.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        LamplitView _lamplit;
        GeneratedLevel _lamplitOf;

        /// <summary>The lamplit view of the floor she is on, once a lamplit level was chosen there; else null.</summary>
        public LamplitView Lamplit => _lamplit != null && _lamplitOf == _level ? _lamplit : null;

        /// <summary>Once a frame (and for tests): the floor she is on lit as the setting says. Allocates only when it changes.</summary>
        public void FollowLamplit()
        {
            if (_level == null) return;
            if (_lamplitOf != _level)
            {
                _lamplitOf = _level;
                _lamplit = null;
            }
            LamplitValues want = GameSettings.Greybox ? null : LightSteps.Of(GameSettings.LightStep);
            if (_lamplit == null)
            {
                if (want == null) return;
                _lamplit = LamplitView.Add(_level);
            }
            _lamplit.Set(want);
        }
    }
}
