using System;

namespace Roguelike.Rules
{
    /// <summary>What the quick duck sorts a prop as (work order 043): low, tall, or slender (neither).</summary>
    public enum PropHeightClass { Low, Tall, Slender }

    /// <summary>
    /// Which props are low and which tall for the quick duck (Kristian's answer of 2026-11-20 to the cover report,
    /// proposal C; work order 043). The report found that the line between tall and low (<see cref="PropValues.CoverHeight"/>,
    /// 1.2 m) falls among props of 1.0 to 1.3 m that cannot be told apart from 28 m up. So:
    ///
    /// - Low: at most <see cref="LowMost"/> tall. It can be ducked behind and never stops a shot by itself.
    /// - Tall: at least <see cref="TallLeast"/>. It stops shots by itself, standing; there is no duck at it.
    /// - In between, each by what it looks like from above (<see cref="LooksLow"/>), read from its mesh's profile (the
    ///   import's vertices, sliced by height; no model was changed). All three are low:
    ///   the altar is a pedestal block (a 1.17 x 1.27 m step to 0.26 m, then a 0.8 x 0.73 m column to about 1.1 m, a
    ///   small piece on top to 1.3 m): a block like the chest and the sarcophagus beside it;
    ///   the grave marker is a headstone (a 1.08 x 0.67 m base to 0.26 m, then an upright stone 0.8 m wide and 0.33 m
    ///   thick to 1.3 m): the thing one crouches behind, thin seen from above, with no mass of a pillar;
    ///   the broken pillar is a stump (a 1.13 m round plinth to 0.24 m, then a shaft only 0.38 m across to 1.2 m): from
    ///   above a wide low plinth with a thin stub, nothing like the 3 m pillar.
    /// - Slender (work order 028, the candelabra): stops no shot whatever its height, and gives no duck (it carries no
    ///   <c>Shelter</c>), as before. So too the two of work order 052 that are walked over or under (the dais, sunk to a
    ///   low platform, and the banner on its wall: <see cref="PropSize.Walkable"/>): neither low nor tall, they stop nothing.
    ///
    /// Work order 052's other six by the same lines: the coin heap (0.63 m) and the candle cluster (1.0) are low; the bone
    /// wall (2.5) and the knight (3.0) tall; the great altar (1.82) is past the tall line by its height, and from above its
    /// 3 x 1.5 m slab with the basin is the whole of it, a block of stone: tall. The great brazier (1.5 m) lies between the
    /// lines and is not named low: from above its widest part is the 1.24 m bowl of coals at its top, a mass as wide as its
    /// footprint, nothing like a plinth with a thin stub; so it keeps the old rule and is tall from the cover height.
    ///
    /// A model added later between the two lines that is not named here keeps the old rule: tall from
    /// <see cref="PropValues.CoverHeight"/>, as it would have been.
    /// </summary>
    public static class PropSort
    {
        /// <summary>Metres: at most this tall, a prop is low.</summary>
        public const double LowMost = 1.1;
        /// <summary>Metres: at least this tall, a prop is tall.</summary>
        public const double TallLeast = 1.8;

        const double Slack = 1e-3;

        /// <summary>The sort of a model, from its table entry (<see cref="PropSize.Crypt"/>).</summary>
        public static PropHeightClass Of(PropModel model)
        {
            PropSize size = PropSize.Of(model);
            if (size.Slender) return PropHeightClass.Slender;
            if (size.Height <= LowMost + Slack) return PropHeightClass.Low;
            if (size.Height >= TallLeast - Slack) return PropHeightClass.Tall;
            if (LooksLow(model)) return PropHeightClass.Low;
            return size.Height >= new PropValues().CoverHeight - Slack ? PropHeightClass.Tall : PropHeightClass.Low;
        }

        /// <summary>The props between the two lines that look low from above (see the class).</summary>
        public static bool LooksLow(PropModel model) =>
            model == PropModel.Altar || model == PropModel.GraveMarker || model == PropModel.PillarBroken;

        /// <summary>
        /// A prop that stops shots by its height today (<see cref="LevelProp.IsCover"/>) but that the quick duck sorts low:
        /// with the duck's switch on it stops nothing and can be ducked behind.
        /// </summary>
        public static bool LowerUnderTheDuck(LevelProp prop, PropValues v) => prop.IsCover(v) && Of(prop.Model) == PropHeightClass.Low;
    }

    /// <summary>What became of a tap of the cover key with the quick duck on (work order 043).</summary>
    public enum DuckTap
    {
        /// <summary>She ducked.</summary>
        Ducked,
        /// <summary>No low prop within reach: nothing happens, and no wait.</summary>
        NothingInReach,
        /// <summary>She is down already.</summary>
        Down,
        /// <summary>The key waits after the last duck.</summary>
        Waiting,
        /// <summary>She cannot duck now: in a skill, a dash or Slide, stunned or pulled, dead, in a choice or paused.</summary>
        Busy,
        /// <summary>The switch is off (or taking cover is).</summary>
        Off,
    }

    /// <summary>How a duck ended.</summary>
    public enum DuckEnd
    {
        /// <summary>She rose by herself when its time was up.</summary>
        Rose,
        /// <summary>She was told to move.</summary>
        Moved,
        /// <summary>She dashed.</summary>
        Dashed,
        /// <summary>Anything else: stunned or pulled, death, a restart, leaving a floor, the switch, the prop gone.</summary>
        Other,
        /// <summary>The held duck (work order 086): the key went up.</summary>
        Released,
        /// <summary>The held duck (work order 086): she came up to shoot or to use a skill, which goes off at once.</summary>
        Acted,
    }

    /// <summary>
    /// The held duck (work order 086; Kristian's answer of 2026-11-22, answers file row 5: "Plasser dekning for den, men vil at den
    /// skal være hold for duck. Så kan man gjøre det så lenge man vil"), with <see cref="FeelValues.DuckHold"/> on, in place of the
    /// tap of work order 043. No engine; the state is <see cref="QuickDuck"/>'s, its down time without end.
    ///
    /// - The cover key going down with a low prop in reach (as before: <see cref="FeelValues.DuckReach"/>, her middle to its
    ///   edge) puts her down where she stands, and she stays down as long as the key is held, with no longest time. Let go, she
    ///   is up at once; from that moment she cannot go down again for <see cref="FeelValues.DuckHoldWait"/> (0.2 s), so it cannot
    ///   be flickered for free: a press in that wait is kept, and puts her down when it is over if the key is still held. With
    ///   nothing in reach the press does nothing, as before; holding the key on and walking up to a prop does not put her down (a
    ///   press does, the third review's rule of work order 047 for a key held through a new floor).
    /// - While she is down the move keys do nothing: she stays at the prop. The dash ends it at once, as before.
    /// - A click or a skill pressed while she is down brings her up at once and it goes off (<see cref="ActEnds"/>); the timing's
    ///   reward is as before: her first basic shot within <see cref="FeelValues.DuckPeekWindow"/> of rising from a duck that
    ///   stopped something staggers. A skill on its cooldown does not bring her up. With the key still held she goes down again
    ///   at the same prop once she is free and the wait is over (<see cref="DownAgain"/>; <see cref="FeelValues.DuckHoldDownAgain"/>).
    /// - What it stops is as before: arrows only (<see cref="QuickDuckRules.Shields"/>).
    /// </summary>
    public static class HeldDuckRules
    {
        /// <summary>Is the duck held (its switch and the quick duck's both on)?</summary>
        public static bool Held(FeelValues f) => f != null && f.QuickDuck && f.DuckHold;

        /// <summary>The seconds a duck lasts by itself: without end when it is held, else the tap's <see cref="FeelValues.DuckSeconds"/>.</summary>
        public static float Seconds(FeelValues f) => Held(f) ? float.PositiveInfinity : f.DuckSeconds;

        /// <summary>The wait from the moment she is up until she can go down again: the held duck's, else the tap's.</summary>
        public static float Wait(FeelValues f) => Held(f) ? f.DuckHoldWait : f.DuckWait;

        /// <summary>Does a held duck end now because the key is up? Always when it is up; never with the tap.</summary>
        public static bool Released(FeelValues f, bool keyHeld) => Held(f) && !keyHeld;

        /// <summary>Does a move key end a duck? With the tap, by <see cref="QuickDuckRules.MoveEnds"/>; held, never: she stays at the prop.</summary>
        public static bool MoveMayEnd(FeelValues f) => !Held(f);

        /// <summary>
        /// Does a press of a shot or a skill bring her up (and it goes off at once)? Held, when it can go off now: a click always,
        /// a skill when it is ready (or a recast in its window); with the tap never: it waits until she rises.
        /// </summary>
        public static bool ActEnds(FeelValues f, bool down, bool wouldGoOff) => Held(f) && down && wouldGoOff;

        /// <summary>
        /// Does she go down again now, the key still held after her own shot or skill brought her up? Held and its switch on, up,
        /// the key held all along since, the wait over, free (no shot, skill, dash, stun or pull, nothing waiting to go off), and
        /// a low prop in reach.
        /// </summary>
        public static bool DownAgain(FeelValues f, bool keyHeld, bool roseToAct, bool down, bool ready, bool free, bool lowPropInReach) =>
            Held(f) && f.DuckHoldDownAgain && keyHeld && roseToAct && !down && ready && free && lowPropInReach;
    }

    /// <summary>
    /// The quick duck (Kristian's answer of 2026-11-20 to the cover report: proposal C, "duck the shot"; work order 043),
    /// in place of taking cover while its switch is on (<see cref="FeelValues.QuickDuck"/>). A moment, not a stance.
    ///
    /// - She taps the cover key. With a low prop (<see cref="PropSort"/>) within <see cref="FeelValues.DuckReach"/> of her
    ///   (her middle to its edge) she drops where she stands for <see cref="FeelValues.DuckSeconds"/> and rises by herself.
    ///   Nothing moves her: no snap, no slide. From the moment she is up again, however the duck ended, the key waits
    ///   <see cref="FeelValues.DuckWait"/>. With no low prop in reach the tap does nothing at all and starts no wait. A wall
    ///   or a tall prop gives no duck: it stops what flies already, standing.
    /// - While she is down she cannot shoot or use a skill (presses wait as at the end of a lock); she can be told to move,
    ///   and a move key that was not held at the tap (<see cref="MoveEnds"/>) or a dash ends the duck at once.
    /// - While she is down, the prop she ducked at stops an arrow (single, fan, volley) whose line from where it was loosed
    ///   to her passes through it (<see cref="Shields"/>: <see cref="TakeCoverRules.Shields"/>, ducked behind a low
    ///   prop, as what she ducked behind stops a shot today). Orbs pass over (they are too large, and they are about
    ///   moving); the boss's wave, melee, the guard's Drag and the brute's Shield bash do not look at it.
    /// - The timing's reward (<see cref="FeelValues.DuckPeekShot"/>): her first basic shot started within
    ///   <see cref="FeelValues.DuckPeekWindow"/> of rising from a duck that stopped something staggers (<see cref="TakePeekShot"/>).
    ///
    /// Times are the game's seconds, passed in.
    /// </summary>
    public sealed class QuickDuck
    {
        /// <summary>True while she is down.</summary>
        public bool IsDown { get; private set; }
        /// <summary>When the duck going on began, and when she rises by herself.</summary>
        public float DownAt { get; private set; } = float.NegativeInfinity;
        public float UpAt { get; private set; } = float.NegativeInfinity;
        /// <summary>When the key works again; minus infinity before the first duck.</summary>
        public float ReadyAt { get; private set; } = float.NegativeInfinity;
        /// <summary>When she last rose from a duck.</summary>
        public float RoseAt { get; private set; } = float.NegativeInfinity;
        /// <summary>What the duck going on has stopped so far.</summary>
        public int Stopped { get; private set; }
        /// <summary>What the last duck that ended stopped.</summary>
        public int StoppedLast { get; private set; }
        bool _peekLeft; // the last duck stopped something and her first shot after it has not started yet

        /// <summary>True when a tap now would duck her, a low prop given: up, and the key's wait over.</summary>
        public bool Ready(float now) => !IsDown && now >= ReadyAt;

        /// <summary>Seconds until the key works again; 0 when it does (or while she is down).</summary>
        public float WaitLeft(float now) => IsDown ? 0f : Math.Max(0f, ReadyAt - now);

        /// <summary>
        /// A tap of the key at <paramref name="now"/>: down already, the key waiting, she may not duck now, no low prop in
        /// reach, or down for <paramref name="seconds"/>. Only the last starts anything.
        /// </summary>
        public DuckTap Tap(float now, bool mayDuck, bool lowPropInReach, float seconds)
        {
            if (IsDown) return DuckTap.Down;
            if (now < ReadyAt) return DuckTap.Waiting;
            if (!mayDuck) return DuckTap.Busy;
            if (!lowPropInReach) return DuckTap.NothingInReach;
            IsDown = true;
            DownAt = now;
            UpAt = now + Math.Max(0f, seconds);
            Stopped = 0;
            return DuckTap.Ducked;
        }

        /// <summary>True when the duck going on is over by its time at <paramref name="now"/>: she rises by herself.</summary>
        public bool RisesBy(float now) => IsDown && now >= UpAt;

        /// <summary>The duck stopped one more thing that flew at her.</summary>
        public void StoppedOne()
        {
            if (IsDown) Stopped++;
        }

        /// <summary>
        /// The duck going on ends: she is up at <paramref name="at"/> (its own time when she rose by herself), and the key
        /// waits <paramref name="wait"/> from then. False when she was not down.
        /// </summary>
        public bool End(float at, float wait)
        {
            if (!IsDown) return false;
            IsDown = false;
            RoseAt = at;
            ReadyAt = at + Math.Max(0f, wait);
            StoppedLast = Stopped;
            _peekLeft = Stopped > 0;
            Stopped = 0;
            return true;
        }

        /// <summary>
        /// A basic shot starts at <paramref name="now"/>: true when it is her first since she rose from a duck that stopped
        /// something and it starts within <paramref name="window"/> of rising. Either way her first shot has had its turn.
        /// </summary>
        public bool TakePeekShot(float now, float window)
        {
            if (IsDown) return false;
            bool peek = _peekLeft && now >= RoseAt && now - RoseAt <= window + 1e-4f;
            _peekLeft = false;
            return peek;
        }

        /// <summary>No duck, no wait and no reward waiting: death, a restart, a new floor.</summary>
        public void Clear()
        {
            IsDown = false;
            DownAt = UpAt = ReadyAt = RoseAt = float.NegativeInfinity;
            Stopped = StoppedLast = 0;
            _peekLeft = false;
        }
    }

    /// <summary>The rules of the quick duck that need no state (work order 043; see <see cref="QuickDuck"/>).</summary>
    public static class QuickDuckRules
    {
        /// <summary>Is a low prop of this footprint within <paramref name="reach"/> of her (her middle to its edge)?</summary>
        public static bool InReach(CoverShape lowProp, GroundPoint her, float reach) => TakeCoverRules.InReach(lowProp, her, reach);

        /// <summary>Does a duck stop this kind of thing that flies? Arrows only: orbs pass over a low prop, and the wave runs under.</summary>
        public static bool Stops(ProjectileKind kind) => kind == ProjectileKind.Arrow;

        /// <summary>
        /// Is she, down beside the low prop <paramref name="lowProp"/>, covered against a thing of <paramref name="kind"/>
        /// loosed from <paramref name="from"/>? An arrow, and the prop on the straight line between the two.
        /// </summary>
        public static bool Shields(CoverShape lowProp, GroundPoint her, GroundPoint from, ProjectileKind kind) =>
            Stops(kind) && TakeCoverRules.Shields(lowProp, tall: false, ducked: true, her, from);

        /// <summary>
        /// Does the move key held now end a duck? Only a direction that was not held at the tap: she can be told to move,
        /// and moving ends it, but a key held through the tap does not, and letting go of the keys does not.
        /// </summary>
        public static bool MoveEnds(GroundPoint heldAtTap, GroundPoint heldNow)
        {
            if (heldNow.Dot(heldNow) < 1e-6f) return false;
            if (heldAtTap.Dot(heldAtTap) < 1e-6f) return true;
            float a = heldAtTap.Length, b = heldNow.Length;
            return heldAtTap.Dot(heldNow) / (a * b) < 0.999f; // more than about 2.5 degrees off
        }

        /// <summary>
        /// Did a move key go down between two frames of the move keys (each axis -1, 0 or 1, the screen's x and y)? An axis
        /// held now that was not held the same way before (the third review of 2026-11-20, work order 047): letting go of
        /// one of two keys is no new press, and does not end a duck.
        /// </summary>
        public static bool MoveKeyWentDown(float beforeX, float beforeY, float nowX, float nowY) =>
            (Math.Sign(nowX) != 0 && Math.Sign(nowX) != Math.Sign(beforeX)) || (Math.Sign(nowY) != 0 && Math.Sign(nowY) != Math.Sign(beforeY));
    }
}
