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

        /// <summary>
        /// The clean part of press <paramref name="press"/> of a chain of <paramref name="count"/> (work order 069): exactly
        /// <see cref="RecastRhythm.Press(KitValues, float, bool, int, int, float)"/>'s, with its window there.
        /// </summary>
        public static CleanPart Of(KitValues kit, float skillWindow, bool openingMove, float wideWindow, int press, int count) =>
            RecastRhythm.Press(kit, skillWindow, openingMove, press, count, wideWindow).Clean;
    }

    /// <summary>
    /// How a press of a recast chain fell (work order 069), for the line of the chain the interface draws
    /// (<see cref="RecastLine"/>) and for the play session's log. Judged by the press's own window and clean part
    /// (<see cref="RecastRhythm"/>).
    /// </summary>
    public enum RecastTiming
    {
        /// <summary>Not pressed (yet).</summary>
        None,
        /// <summary>In its clean part: the last press a clean tap (or a clean spend), a press before it in rhythm (toward a full clean chain).</summary>
        Clean,
        /// <summary>In its window, before its clean part: its shot, not clean.</summary>
        Early,
        /// <summary>In its window, after its clean part: its shot, not clean.</summary>
        Late,
        /// <summary>Pressed before its window opened (the shot before had not gone off): it waited and fired when the window opened, never clean.</summary>
        Waited,
        /// <summary>Its window ran out with no press: the chain ended there.</summary>
        Missed,
        /// <summary>A shot of a skill with no clean press: nothing to judge it by.</summary>
        Plain,
    }

    /// <summary>
    /// One press of a recast chain (work order 069): which press it is (from 1: the first press starts the chain, so the
    /// recast presses are 2 on), its window and its clean part, both in seconds after the shot before it went off (after
    /// Backstep's hop, for its shot), on the window's clock.
    /// </summary>
    public readonly struct RecastBeat
    {
        public readonly int Press;
        public readonly float Window;
        public readonly CleanPart Clean;
        /// <summary>True for the chain's last press: the one whose clean press is a clean tap (or a clean spend).</summary>
        public readonly bool Last;

        public RecastBeat(int press, float window, CleanPart clean, bool last)
        {
            Press = press;
            Window = window;
            Clean = clean;
            Last = last;
        }

        /// <summary>True for a recast press (2 on): false for the default, the first press, and a press past the chain's count.</summary>
        public bool Any => Press >= 2;
    }

    /// <summary>
    /// The rhythm of a recast chain (work order 069, Kristian, 2026-11-21 evening: "jeg vil at de to første skal kome relativt
    /// fort, og det 3. litt senere. men de første er veldig utydlig når skal skje"): THE one function that gives a press its
    /// window and its clean part. The judging, the interface (the strip's box, the cast blade, the signs and their tick, the
    /// combo bar), Wide window's widening, the card's numbers and the test pilot all read it, so what she is shown is what
    /// is judged.
    ///
    /// A chain that opens with a shot (Triple Tap) has its rhythm with <see cref="KitValues.ChainRhythm"/> on: every press
    /// before the last is a quick press (<see cref="KitValues.QuickPressWindow"/>, its clean part
    /// <see cref="KitValues.QuickPressCleanFrom"/> to <see cref="KitValues.QuickPressCleanTo"/>), and the last waits a beat
    /// (<see cref="KitValues.LastPressWindow"/>, <see cref="KitValues.LastPressCleanFrom"/> to
    /// <see cref="KitValues.LastPressCleanTo"/>). A chain that opens with a move (Backstep) and every chain with the switch off
    /// are as before: every window the skill's <see cref="SkillDefinition.RecastWindow"/>, its clean part where
    /// <see cref="CleanPart.Of(KitValues, float, bool)"/> places it. Each part is then widened by Wide window round its middle,
    /// inside its own window (<see cref="WideWindow.Widen(CleanPart, float, float)"/>); 0 widens nothing.
    /// </summary>
    public static class RecastRhythm
    {
        /// <summary>The most presses a chain records the timing of (Triple Tap with Fourth Tap and Steady Hand has six).</summary>
        public const int MostPresses = 16;

        /// <summary>
        /// Press <paramref name="press"/> (2 on) of a chain of <paramref name="count"/> presses: its window and clean part.
        /// <paramref name="skillWindow"/>: the skill's <see cref="SkillDefinition.RecastWindow"/>; <paramref name="openingMove"/>:
        /// the chain opens with a move; <paramref name="wideWindow"/>: her Wide window (0: none). The default for a press that
        /// is no recast press (below 2, or past the count).
        /// </summary>
        public static RecastBeat Press(KitValues kit, float skillWindow, bool openingMove, int press, int count, float wideWindow)
        {
            kit = kit ?? new KitValues();
            if (press < 2 || press > count) return default;
            bool last = press >= count;
            float window;
            CleanPart clean;
            if (kit.ChainRhythm && !openingMove)
            {
                float own = last ? kit.LastPressWindow : kit.QuickPressWindow;
                window = Math.Max(0f, own >= 0f ? own : skillWindow);
                float from = last ? kit.LastPressCleanFrom : kit.QuickPressCleanFrom;
                float to = last ? kit.LastPressCleanTo : kit.QuickPressCleanTo;
                clean = CleanPart.Early(window, from >= 0f ? from : kit.CleanFrom, to >= 0f ? to : kit.CleanTo);
            }
            else
            {
                window = Math.Max(0f, skillWindow);
                clean = CleanPart.Of(kit, window, openingMove);
            }
            return new RecastBeat(press, window, WideWindow.Widen(clean, window, wideWindow), last);
        }

        /// <summary>As above, for press <paramref name="press"/> of this skill's chain as the skill is (its count, its window, its opening move).</summary>
        public static RecastBeat Press(KitValues kit, SkillDefinition skill, int press, float wideWindow) =>
            skill == null ? default : Press(kit, skill.RecastWindow, skill.OpeningMove, press, skill.RecastCount, wideWindow);

        /// <summary>
        /// The whole chain of this skill on one time line (for the interface's combo bar): its recast presses, 2 to its count,
        /// into <paramref name="into"/> from index 0 (as many as fit), each counted from the shot before it. Returns how many
        /// it wrote. Nothing is made: the caller keeps the array.
        /// </summary>
        public static int Line(SkillDefinition skill, KitValues kit, float wideWindow, RecastBeat[] into)
        {
            if (skill == null || into == null) return 0;
            int n = 0;
            for (int press = 2; press <= skill.RecastCount && n < into.Length; press++) into[n++] = Press(kit, skill, press, wideWindow);
            return n;
        }

        /// <summary>
        /// How a press now falls (work order 069): <paramref name="sinceOpened"/> seconds after its window opened (below 0: it
        /// has not), in a window with this clean part; <paramref name="clean"/>: the judging found it clean (a clean tap, or a
        /// press before the last in its clean part); <paramref name="buffered"/>: it waited from before the window opened.
        /// <paramref name="hasCleanPress"/>: the skill has a clean press at all.
        /// </summary>
        public static RecastTiming TimingOf(float sinceOpened, CleanPart part, bool clean, bool buffered, bool hasCleanPress)
        {
            if (clean) return RecastTiming.Clean;
            if (!hasCleanPress) return RecastTiming.Plain;
            if (buffered || sinceOpened < 0f) return RecastTiming.Waited;
            if (!part.Any) return RecastTiming.Plain;
            return part.IsAfter(sinceOpened) ? RecastTiming.Late : RecastTiming.Early;
        }
    }

    /// <summary>
    /// A recast chain on one time line (work order 069), for the interface's combo bar (work order 068 draws it): for each
    /// press its window and clean part (<see cref="Beat"/>, from <see cref="RecastRhythm"/>, as judged), and for a chain in
    /// progress which press is next, how long since the shot before it went off, and how each press already made fell
    /// (<see cref="Timing"/>). Made from a skill alone (<see cref="Of(SkillDefinition, KitValues, float)"/>: no chain) or from
    /// the chain running (the character's <c>RecastLine</c>). A value: it reads, holds no list, and makes nothing.
    /// </summary>
    public readonly struct RecastLine
    {
        readonly RecastChain _chain;
        readonly KitValues _kit;
        readonly float _skillWindow, _wide;
        readonly bool _openingMove, _primed, _hasCleanPress;

        /// <summary>The chain's presses, the first included (3 for Triple Tap); 0 for no chain and no skill with one.</summary>
        public readonly int Count;
        /// <summary>The press whose window is open or comes next (2 to <see cref="Count"/>); 0 when none waits (no chain running, or complete).</summary>
        public readonly int Next;
        /// <summary>Seconds since the shot before <see cref="Next"/> went off, on the window's clock; -1 while it has not (its startup) and with none.</summary>
        public readonly float SinceShotBefore;

        RecastLine(RecastChain chain, KitValues kit, float skillWindow, bool openingMove, int count, int next, float since, float wide, bool primed, bool hasCleanPress)
        {
            _chain = chain;
            _kit = kit;
            _skillWindow = skillWindow;
            _openingMove = openingMove;
            Count = count;
            Next = next;
            SinceShotBefore = since;
            _wide = wide;
            _primed = primed;
            _hasCleanPress = hasCleanPress;
        }

        /// <summary>The skill's chain with none running: every press's window and clean part; <see cref="Next"/> 0.</summary>
        public static RecastLine Of(SkillDefinition skill, KitValues kit, float wideWindow) =>
            skill == null || skill.RecastCount < 2 ? default
                : new RecastLine(null, kit, skill.RecastWindow, skill.OpeningMove, skill.RecastCount, 0, -1f, wideWindow, false, RecastChain.HasCleanPress(skill));

        /// <summary>
        /// The chain running, of this skill (as she has it): its count and opening move are the chain's own.
        /// <paramref name="primed"/>: Hair Trigger's priming lies on it (its last press is clean anywhere in its window).
        /// </summary>
        public static RecastLine Of(RecastChain chain, SkillDefinition skill, KitValues kit, float wideWindow, bool primed, float now)
        {
            if (chain == null || chain.Slot < 0 || skill == null) return default;
            int next = chain.IsComplete || chain.Expired(now) ? 0 : chain.Shots + 1;
            return new RecastLine(chain, kit, skill.RecastWindow, chain.OpenedWithMove, chain.Count, next, next > 0 ? chain.SinceOpened(now) : -1f,
                                  wideWindow, primed, RecastChain.HasCleanPress(skill));
        }

        /// <summary>True when there is a chain to show: a skill with one, or one running.</summary>
        public bool Any => Count >= 2;

        /// <summary>
        /// Press <paramref name="press"/>'s window and clean part, from the shot before it: no clean part for a skill with no
        /// clean press, and a primed chain's last press clean in its whole window. The default for a press that is no recast press.
        /// </summary>
        public RecastBeat Beat(int press)
        {
            RecastBeat beat = RecastRhythm.Press(_kit, _skillWindow, _openingMove, press, Count, _wide);
            if (!beat.Any) return beat;
            if (!_hasCleanPress) return new RecastBeat(beat.Press, beat.Window, default, beat.Last);
            return _primed && beat.Last ? new RecastBeat(beat.Press, beat.Window, CleanPart.Whole(beat.Window), true) : beat;
        }

        /// <summary>How press <paramref name="press"/> of the chain running fell; <see cref="RecastTiming.None"/> before it is made, and with no chain.</summary>
        public RecastTiming Timing(int press) => _chain != null ? _chain.TimingOf(press) : RecastTiming.None;
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
    ///
    /// Work order 069: each press may have its own window (<see cref="NextWindow"/>, from <see cref="RecastRhythm"/>: Triple
    /// Tap's quick presses, then its last a beat later), and the chain keeps how each press fell (<see cref="TimingOf"/>).
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
        // Work order 069: how each press of the chain fell, by its number (2 on); kept after the chain ends, until the next starts.
        readonly RecastTiming[] _timings = new RecastTiming[RecastRhythm.MostPresses + 1];

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
            Array.Clear(_timings, 0, _timings.Length);
        }

        /// <summary>
        /// The window for the next press is this long (work order 069: each press its own, from <see cref="RecastRhythm"/>):
        /// set by the caller after <see cref="Start"/> and after each <see cref="Recast(bool)"/>, while the window is closed.
        /// Nothing while one is open, or with no chain. Never called, every window is the one <see cref="Start"/> was given.
        /// </summary>
        public void NextWindow(float window)
        {
            if (_slot < 0 || !float.IsPositiveInfinity(_openedAt)) return;
            _window = Math.Max(0f, window);
        }

        /// <summary>How press <paramref name="press"/> (2 on) of the chain fell (work order 069); kept after the chain ends, until the next starts.</summary>
        public RecastTiming TimingOf(int press) => press >= 2 && press < _timings.Length ? _timings[press] : RecastTiming.None;

        /// <summary>The window for the next press ran out with no press (work order 069): that press is missed. The caller then ends the chain.</summary>
        public void MarkMissed()
        {
            if (_slot < 0 || _shots >= _count) return;
            int press = _shots + 1;
            if (press < _timings.Length) _timings[press] = RecastTiming.Missed;
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
        public void Recast(bool inCleanPart) => Recast(inCleanPart, inCleanPart ? RecastTiming.Clean : RecastTiming.None);

        /// <summary>As above, and how the press fell (work order 069), kept as the timing of the press it makes (<see cref="TimingOf"/>).</summary>
        public void Recast(bool inCleanPart, RecastTiming timing)
        {
            if (_slot < 0) return;
            if (_shots + 1 < _timings.Length) _timings[_shots + 1] = timing;
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
