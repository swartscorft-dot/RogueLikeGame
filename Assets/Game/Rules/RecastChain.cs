using System;

namespace Roguelike.Rules
{
    /// <summary>What a press of a recast skill's key is, judged by <see cref="RecastChain.Judge"/>.</summary>
    public enum RecastPress
    {
        /// <summary>No chain waits for this press: it is an ordinary press of the skill.</summary>
        None,
        /// <summary>The chain's last shot has not gone off yet, so no window is open: the press waits like any press during a skill.</summary>
        Early,
        /// <summary>Inside the window: the next shot.</summary>
        Shot,
        /// <summary>Inside the last share of the final window: the next shot, and a clean tap.</summary>
        Clean,
    }

    /// <summary>
    /// A skill pressed several times in a row (decision 0005, 2026-11-19; Triple Tap first). The first press fires the
    /// first shot; when a shot has gone off, a window of <see cref="SkillDefinition.RecastWindow"/> opens, and a press
    /// of the same key inside it fires the next shot, up to <see cref="SkillDefinition.RecastCount"/> shots. After the
    /// last shot, or when a window runs out, the chain is over. The press that fires the last shot is clean when it
    /// falls in the last <see cref="KitValues.CleanShare"/> of its window and the skill has a clean tap
    /// (<see cref="SkillDefinition.CleanTapMarks"/> above 0) or a clean spend (<see cref="SkillDefinition.CleanMarkDamageBonus"/>
    /// above 0). A press after the window has run out is no shot: the chain is over. Time is passed in, in seconds.
    ///
    /// A chain may open with a move (<see cref="SkillDefinition.OpeningMove"/>; Backstep, amended 2026-11-19 after
    /// Kristian played): the first press is a move that fires nothing, and its window opens when the move has ended,
    /// as a shot's does when it has gone off. The presses still count from the first: Backstep's chain of two is the
    /// hop and one shot. <see cref="IsOpeningMove"/> says the use in progress is that move.
    /// </summary>
    public sealed class RecastChain
    {
        int _slot = -1;
        int _count;
        int _shots;
        float _window;
        float _openedAt = float.PositiveInfinity;
        bool _openingMove;

        /// <summary>The slot whose chain runs, or -1. A chain that has run out still shows here until <see cref="Expired"/> or <see cref="End"/>.</summary>
        public int Slot => _slot;
        /// <summary>Shots of the chain fired, or started, so far: 1 after the first press.</summary>
        public int Shots => _shots;
        public int Count => _count;
        /// <summary>True once the shot before has gone off and the window for the next press is open, until it runs out.</summary>
        public bool IsWindowOpen(float now) => _slot >= 0 && now >= _openedAt && now <= _openedAt + _window;
        /// <summary>True when the open window is for the last shot of the chain.</summary>
        public bool NextIsLast => _shots == _count - 1;

        /// <summary>
        /// The first press of a skill. A skill with a count of 1 has no chain (and so no opening move: its one press
        /// is an ordinary use). <paramref name="openingMove"/>: the first press is a move that fires nothing.
        /// </summary>
        public void Start(int slot, int count, float window, bool openingMove = false)
        {
            _count = Math.Max(1, count);
            _slot = _count > 1 ? slot : -1;
            _shots = 1;
            _window = Math.Max(0f, window);
            _openedAt = float.PositiveInfinity;
            _openingMove = openingMove && _slot >= 0;
        }

        /// <summary>True while the chain is on its first press and that press is a move that fires nothing.</summary>
        public bool IsOpeningMove => _slot >= 0 && _openingMove && _shots == 1;

        /// <summary>The shots fired, or started, so far: the presses, less the opening move if the chain has one.</summary>
        public int ShotsFired => _openingMove ? _shots - 1 : _shots;

        /// <summary>
        /// The chain's current shot went off, or its opening move ended: the window for the next press opens now.
        /// Nothing after the last shot: the chain is then <see cref="IsComplete"/>, and the caller ends it.
        /// </summary>
        public void ShotWentOff(float now)
        {
            if (_slot < 0 || _shots >= _count) return;
            if (float.IsPositiveInfinity(_openedAt)) _openedAt = now;
        }

        /// <summary>True when every shot of the chain has been pressed: nothing waits for another press.</summary>
        public bool IsComplete => _slot >= 0 && _shots >= _count;

        /// <summary>True when a window was open and has run out by now: the chain is over, short of its count.</summary>
        public bool Expired(float now) => _slot >= 0 && !float.IsPositiveInfinity(_openedAt) && now > _openedAt + _window;

        /// <summary>What a press of this slot's key is, now.</summary>
        public RecastPress Judge(int slot, float now, float cleanShare, bool hasCleanTap)
        {
            if (_slot < 0 || slot != _slot || Expired(now)) return RecastPress.None;
            if (now < _openedAt) return RecastPress.Early;
            bool last = _shots + 1 >= _count;
            return last && hasCleanTap && IsClean(now - _openedAt, _window, cleanShare) ? RecastPress.Clean : RecastPress.Shot;
        }

        /// <summary>True when the last press of this skill's chain can be clean: it has a clean tap or a clean spend.</summary>
        public static bool HasCleanPress(SkillDefinition skill) =>
            skill != null && (skill.CleanTapMarks > 0 || skill.CleanMarkDamageBonus > 0f);

        /// <summary>True when a press this long after its window opened falls in the window's last share.</summary>
        public static bool IsClean(float sinceOpened, float window, float cleanShare)
        {
            float share = Math.Max(0f, Math.Min(1f, cleanShare));
            return sinceOpened <= window && sinceOpened >= window * (1f - share) - 1e-5f;
        }

        /// <summary>A press fired the next shot: the window closes until that shot goes off.</summary>
        public void Recast()
        {
            if (_slot < 0) return;
            _shots++;
            _openedAt = float.PositiveInfinity;
        }

        /// <summary>Ends the chain, whatever its state (a dash, another skill, a run-out window). Returns the shots it fired.</summary>
        public int End()
        {
            int shots = _shots;
            _slot = -1;
            _openedAt = float.PositiveInfinity;
            return shots;
        }

        /// <summary>For a bar, 1 to 0: how much of the open window is left. 0 when none is open.</summary>
        public float WindowLeft(float now)
        {
            if (!IsWindowOpen(now) || _window <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - (now - _openedAt) / _window));
        }

        /// <summary>
        /// The window's length, in seconds, of the chain running (work order 037: the signs of the clean press read it).
        /// 0 with no chain.
        /// </summary>
        public float Window => _slot >= 0 ? _window : 0f;

        /// <summary>
        /// Seconds since the window for the next press opened (work order 037), past its length once it has run out; -1
        /// while the shot before has not gone off yet, and with no chain. It reads; it changes nothing.
        /// </summary>
        public float SinceOpened(float now) => _slot < 0 || float.IsPositiveInfinity(_openedAt) ? -1f : now - _openedAt;
    }
}
