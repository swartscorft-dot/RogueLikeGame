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
        /// <summary>Inside the clean part of the final window (<see cref="CleanPart"/>): the next shot, and a clean tap.</summary>
        Clean,
    }

    /// <summary>
    /// Where in an open recast window the press of the chain's last shot is clean, in seconds after the window opened,
    /// both edges inside (work order 040, Kristian's second session of 2026-11-20). Until then it was always the window's
    /// last share (<see cref="KitValues.CleanShare"/>, <see cref="Last"/>): 0.40 to 0.60 s after Triple Tap's shot went off,
    /// about 0.55 to 0.75 s after the press, twice his own rhythm of about 0.36 s, so "there is no point completing the F
    /// shot: the timing must come much earlier". Now it is <see cref="KitValues.CleanFrom"/> to <see cref="KitValues.CleanTo"/>
    /// after the window opens (<see cref="Early"/>): his rhythm, held a hair. A press after it and before the window ends
    /// is a plain shot, late (<see cref="IsAfter"/>). <see cref="Of"/> picks the placement the switch
    /// (<see cref="KitValues.CleanEarly"/>) says.
    /// </summary>
    public readonly struct CleanPart
    {
        /// <summary>Seconds after the window opened that the clean part begins, and ends.</summary>
        public readonly float From, To;

        public CleanPart(float from, float to)
        {
            From = from;
            To = to;
        }

        /// <summary>True when there is a clean part at all.</summary>
        public bool Any => To > From;

        /// <summary>
        /// True when a press this long after the window opened is in the clean part: from its start (a hair early counts,
        /// as before) to its end, both inside. For the last share, exactly <see cref="RecastChain.IsClean"/>.
        /// </summary>
        public bool Contains(float sinceOpened) => Any && sinceOpened >= From - Edge && sinceOpened <= To + Edge;

        /// <summary>True when a press this long after the window opened comes after the clean part: late (work order 040).</summary>
        public bool IsAfter(float sinceOpened) => Any && sinceOpened > To + Edge;

        const float Edge = 1e-5f; // s: the clock's rounding at either edge (at its end too since work order 040; a window run out is no press at all)

        /// <summary>The placement before work order 040: the window's last share.</summary>
        public static CleanPart Last(float window, float cleanShare)
        {
            float w = Math.Max(0f, window);
            float share = Math.Max(0f, Math.Min(1f, cleanShare));
            return new CleanPart(w * (1f - share), w);
        }

        /// <summary>The placement of work order 040: from <paramref name="from"/> to <paramref name="to"/> seconds after the window opened, inside it.</summary>
        public static CleanPart Early(float window, float from, float to)
        {
            float w = Math.Max(0f, window);
            float f = Math.Max(0f, Math.Min(w, from));
            return new CleanPart(f, Math.Max(f, Math.Min(w, to)));
        }

        /// <summary>The whole window (Hair Trigger's priming, work order 025: the timing counts as right anywhere in it).</summary>
        public static CleanPart Whole(float window) => new CleanPart(0f, Math.Max(0f, window));

        /// <summary>The clean part of a window this long, as the kit's switch places it: early (work order 040) or the last share.</summary>
        public static CleanPart Of(KitValues kit, float window) => Of(kit, window, false);

        /// <summary>
        /// As above, for a chain that opened with a move (<paramref name="afterOpeningMove"/>: Backstep's shot after its hop):
        /// work order 064 moves its clean part early, to <see cref="KitValues.OpeningMoveCleanFrom"/> to
        /// <see cref="KitValues.OpeningMoveCleanTo"/> after the hop's end; each below 0 is the kit's own, as before.
        /// </summary>
        public static CleanPart Of(KitValues kit, float window, bool afterOpeningMove)
        {
            kit = kit ?? new KitValues();
            if (!kit.CleanEarly) return Last(window, kit.CleanShare);
            float from = afterOpeningMove && kit.OpeningMoveCleanFrom >= 0f ? kit.OpeningMoveCleanFrom : kit.CleanFrom;
            float to = afterOpeningMove && kit.OpeningMoveCleanTo >= 0f ? kit.OpeningMoveCleanTo : kit.CleanTo;
            return Early(window, from, to);
        }

        /// <summary>
        /// As above, widened by her Wide window (work order 067, <see cref="WideWindow.Widen(CleanPart, float, float)"/>): round
        /// its middle, inside the window; <paramref name="wideWindow"/> 0 (not taken, the stats off) is exactly the part above.
        /// </summary>
        public static CleanPart Of(KitValues kit, float window, bool afterOpeningMove, float wideWindow) =>
            WideWindow.Widen(Of(kit, window, afterOpeningMove), window, wideWindow);

        /// <summary>Its length in seconds.</summary>
        public float Width => Any ? To - From : 0f;
    }

    /// <summary>
    /// A skill pressed several times in a row (decision 0005, 2026-11-19; Triple Tap first). The first press fires the
    /// first shot; when a shot has gone off, a window of <see cref="SkillDefinition.RecastWindow"/> opens, and a press
    /// of the same key inside it fires the next shot, up to <see cref="SkillDefinition.RecastCount"/> shots. After the
    /// last shot, or when a window runs out, the chain is over. The press that fires the last shot is clean when it
    /// falls in the clean part of its window (<see cref="CleanPart"/>: since work order 040 early in it, before that its
    /// last <see cref="KitValues.CleanShare"/>), is no press that waited from before the window opened (a buffered press
    /// fires the instant it opens: mashing never counts), and the skill has a clean tap
    /// (<see cref="SkillDefinition.CleanTapMarks"/> above 0) or a clean spend (<see cref="SkillDefinition.CleanMarkDamageBonus"/>
    /// above 0). A press after the window has run out is no shot: the chain is over. Time is passed in, in seconds.
    ///
    /// A chain may open with a move (<see cref="SkillDefinition.OpeningMove"/>; Backstep, amended 2026-11-19 after
    /// Kristian played): the first press is a move that fires nothing, and its window opens when the move has ended,
    /// as a shot's does when it has gone off. The presses still count from the first: Backstep's chain of two is the
    /// hop and one shot. <see cref="IsOpeningMove"/> says the use in progress is that move.
    ///
    /// An open window's clock is the time passed in, run on by <see cref="RunOn"/> (work order 038, Kristian's answer of
    /// 2026-11-20: the window runs on real time): the caller adds the seconds a hit stop froze, as
    /// <see cref="RecastClock"/> counts them. With none added the window is as it was.
    /// </summary>
    public sealed class RecastChain
    {
        int _slot = -1;
        int _count;
        int _shots;
        float _window;
        float _openedAt = float.PositiveInfinity;
        bool _openingMove;
        float _ranOn; // s the open window has run on beyond the time passed in (work order 038); 0 until it opens
        bool _allInRhythm; // every press of the chain after the first fell in the clean part of its window (work order 064)

        /// <summary>The slot whose chain runs, or -1. A chain that has run out still shows here until <see cref="Expired"/> or <see cref="End"/>.</summary>
        public int Slot => _slot;
        /// <summary>Shots of the chain fired, or started, so far: 1 after the first press.</summary>
        public int Shots => _shots;
        public int Count => _count;
        /// <summary>True once the shot before has gone off and the window for the next press is open, until it runs out.</summary>
        public bool IsWindowOpen(float now) => _slot >= 0 && now >= _openedAt && now + _ranOn <= _openedAt + _window;
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
            _ranOn = 0f;
            _openingMove = openingMove && _slot >= 0;
            _allInRhythm = true;
        }

        /// <summary>True when the chain running opened with a move (Backstep's hop): its window's clean part is its own (work order 064).</summary>
        public bool OpenedWithMove => _slot >= 0 && _openingMove;

        /// <summary>
        /// True when every press of the chain after the first has fallen in the clean part of its window, judged as the last
        /// press is (work order 064: a full clean chain); false before any such press, and once one missed it.
        /// </summary>
        public bool AllInRhythm => _slot >= 0 && _allInRhythm && _shots >= 2;

        /// <summary>
        /// True when a press of this slot's key now falls in the clean part of the open window, whichever press of the chain it
        /// is (work order 064: the presses before the last count toward a full clean chain). A press that waited from before
        /// the window opened never does.
        /// </summary>
        public bool InCleanPart(int slot, float now, CleanPart clean, bool buffered = false) =>
            _slot >= 0 && slot == _slot && !buffered && !Expired(now) && now >= _openedAt && clean.Contains(now - _openedAt + _ranOn);

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
        public bool Expired(float now) => _slot >= 0 && !float.IsPositiveInfinity(_openedAt) && now + _ranOn > _openedAt + _window;

        /// <summary>What a press of this slot's key is, now, with the clean part the window's last share (as before work order 040).</summary>
        public RecastPress Judge(int slot, float now, float cleanShare, bool hasCleanTap) =>
            Judge(slot, now, CleanPart.Last(_window, cleanShare), hasCleanTap);

        /// <summary>
        /// What a press of this slot's key is, now, with this clean part (work order 040). <paramref name="buffered"/>: the
        /// press waited from before the window opened and fires now; it is a shot, never clean.
        /// </summary>
        public RecastPress Judge(int slot, float now, CleanPart clean, bool hasCleanTap, bool buffered = false)
        {
            if (_slot < 0 || slot != _slot || Expired(now)) return RecastPress.None;
            if (now < _openedAt) return RecastPress.Early;
            bool last = _shots + 1 >= _count;
            return last && hasCleanTap && !buffered && clean.Contains(now - _openedAt + _ranOn) ? RecastPress.Clean : RecastPress.Shot;
        }

        /// <summary>
        /// The open window's clock runs on by these seconds beyond the time passed in (work order 038: the real seconds a
        /// hit stop froze, as <see cref="RecastClock.HitStopSeconds"/> counts them). Nothing while no window is open: its
        /// clock starts when it opens, and a press before that is early however long a hit stop held the shot.
        /// </summary>
        public void RunOn(float seconds)
        {
            if (_slot < 0 || seconds <= 0f || float.IsPositiveInfinity(_openedAt)) return;
            _ranOn += seconds;
        }

        /// <summary>True when the last press of this skill's chain can be clean: it has a clean tap or a clean spend.</summary>
        public static bool HasCleanPress(SkillDefinition skill) =>
            skill != null && (skill.CleanTapMarks > 0 || skill.CleanMarkDamageBonus > 0f);

        /// <summary>True when a press this long after its window opened falls in the window's last share (the placement before work order 040).</summary>
        public static bool IsClean(float sinceOpened, float window, float cleanShare)
        {
            float share = Math.Max(0f, Math.Min(1f, cleanShare));
            return sinceOpened <= window && sinceOpened >= window * (1f - share) - 1e-5f;
        }

        /// <summary>A press fired the next shot: the window closes until that shot goes off.</summary>
        public void Recast() => Recast(false);

        /// <summary>As <see cref="Recast()"/>, and whether the press fell in the clean part of its window (<see cref="AllInRhythm"/>).</summary>
        public void Recast(bool inCleanPart)
        {
            if (_slot < 0) return;
            _allInRhythm &= inCleanPart;
            _shots++;
            _openedAt = float.PositiveInfinity;
            _ranOn = 0f;
        }

        /// <summary>Ends the chain, whatever its state (a dash, another skill, a run-out window). Returns the shots it fired.</summary>
        public int End()
        {
            int shots = _shots;
            _slot = -1;
            _openedAt = float.PositiveInfinity;
            _ranOn = 0f;
            return shots;
        }

        /// <summary>For a bar, 1 to 0: how much of the open window is left. 0 when none is open.</summary>
        public float WindowLeft(float now)
        {
            if (!IsWindowOpen(now) || _window <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - (now - _openedAt + _ranOn) / _window));
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
        public float SinceOpened(float now) => _slot < 0 || float.IsPositiveInfinity(_openedAt) ? -1f : now - _openedAt + _ranOn;
    }

    /// <summary>
    /// Which seconds an open recast window counts (work order 038, Kristian's answer of 2026-11-20: the window runs on
    /// real time). Its clock is game time, and a hit stop freezes game time: every landed shot added 0.05 s and every
    /// landed basic shot 0.025 s of real time, so the clean part moved 25 to 100 ms from one use to the next, and a rhythm
    /// that moves cannot be learnt. Only the hit stop is taken out:
    /// - a frame that ran counts its game seconds, as before (so a slow-down of the whole game slows the window, and a
    ///   test's or a flight's faster game runs it faster);
    /// - a frame a hit stop froze counts its real seconds, slowed with a slow-down of the whole game (the time scale the
    ///   hit stop found, below 1) and never sped up: a hit stop is its real length at any time scale, so counted at a
    ///   faster game's pace it would cost the window four or eight times the share it costs a person at scale 1 (a third
    ///   of Triple Tap's window at 4). Counted unscaled it costs the same share at every scale of 1 or more, and at
    ///   scale 1 the window runs on exactly as if the hit stop had not been;
    /// - a frame anything else froze counts nothing: the pause menu, a choice, an end screen or the screen between runs
    ///   (a screen that holds the game, even over a hit stop), or a time scale of 0 that is no hit stop.
    /// </summary>
    public static class RecastClock
    {
        /// <summary>
        /// The most real seconds one frame may add (the third review of 2026-11-20, work order 047): one very long frame
        /// in a hit stop (a hitch) would otherwise add its whole length and eat the clean part.
        /// </summary>
        public const float MostPerFrame = 0.1f;

        /// <summary>
        /// The seconds a frame adds to an open window beyond its game seconds. <paramref name="on"/>: the switch
        /// (<see cref="KitValues.RecastOnRealTime"/>); off, none, and the window is as it was. <paramref name="hitStopHolds"/>:
        /// a hit stop held this frame's game time; <paramref name="screenHolds"/>: a screen holds the game;
        /// <paramref name="paceBefore"/>: the time scale the hit stop found.
        /// </summary>
        public static float HitStopSeconds(bool on, float gameSeconds, float realSeconds, bool hitStopHolds, bool screenHolds, float paceBefore)
        {
            if (!on || !hitStopHolds || screenHolds || gameSeconds > 0f || realSeconds <= 0f) return 0f;
            return Math.Min(realSeconds, MostPerFrame) * Math.Max(0f, Math.Min(1f, paceBefore));
        }
    }
}
