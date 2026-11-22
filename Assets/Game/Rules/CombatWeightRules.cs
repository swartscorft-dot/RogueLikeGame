using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// How heavy a hit of hers is, as it is heard (work order 080, Kristian's tick of 2026-11-22, "Lyd og følelse i kamp":
    /// more weight in hits, kills and clean presses): the basic shot, a skill's hit, a crit. A heavier one wins a frame.
    /// </summary>
    public enum HitWeight { Basic, Heavy, Crit }

    /// <summary>What a press of a recast chain sounds (work order 080): nothing, a step of the chain, its top note, or the dull tick of a press that was off.</summary>
    public enum ChainTickKind { None, Step, Top, Off }

    /// <summary>One press's tick: its kind and, for a step, which step of the chain it is (from 1).</summary>
    public struct ChainTick
    {
        public ChainTickKind Kind;
        public int Step;
    }

    /// <summary>
    /// Part 1 of work order 080, a hit that lands is heard by its weight. First guesses, made without hearing them.
    /// </summary>
    [Serializable]
    public sealed class HitWeightValues
    {
        /// <summary>Off: her hits tick as this morning (the one hit tick, and the crit's tick over it).</summary>
        public bool On = true;
        /// <summary>A hit of a press in its clean part gets a bright edge over its impact (work order 080).</summary>
        public bool CleanEdge = true;
        /// <summary>
        /// At most this many impacts start in one frame, the heaviest first: a Scatter Blast into a pack is one thick hit,
        /// a little louder for each target more (the crowd rule's merge), not eight.
        /// </summary>
        public int PerFrame = 1;
        /// <summary>...and at most this many in any <see cref="Window"/> seconds, across all weights: Fan Fire's ticks into a crowd stay a rattle, not a wall.</summary>
        public int PerWindow = 3;
        /// <summary>s: the window of <see cref="PerWindow"/>, in real time (a hit stop does not stretch it).</summary>
        public float Window = 0.25f;
        /// <summary>
        /// The variations of each impact: the clip played at these pitches (a lower one is longer too), never the same one
        /// twice in a row. A recorded clip of his gets the same four.
        /// </summary>
        public float[] Pitches = { 1f, 0.91f, 1.09f, 0.84f };
    }

    /// <summary>Part 2 of work order 080, a kill pays. First guesses.</summary>
    [Serializable]
    public sealed class KillWeightValues
    {
        /// <summary>Off: a death is heard as this morning (the enemy's own death thump) and no burst is drawn.</summary>
        public bool On = true;
        /// <summary>A small burst where it stood, in the shot effects' style (code only, pooled).</summary>
        public bool Burst = true;
        /// <summary>The kill sound's variations, as the impacts' (<see cref="HitWeightValues.Pitches"/>).</summary>
        public float[] Pitches = { 1f, 0.94f, 1.06f };
        /// <summary>m: the burst's star at its largest.</summary>
        public float BurstSize = 1.1f;
        /// <summary>s: how long a burst lives.</summary>
        public float BurstSeconds = 0.22f;
        /// <summary>The burst of the kill that empties a room, this many times larger and longer.</summary>
        public float RoomWonScale = 1.7f;
        /// <summary>m over its ground point that the burst is drawn: about the middle of a brute.</summary>
        public float BurstHeight = 1.1f;
        /// <summary>At most this many bursts live at once; a new one takes the oldest's place. Made once, at the first kill.</summary>
        public int BurstPool = 8;
        /// <summary>
        /// s (real time): the stone shift of the way opening (the morning's sound for a combat room cleared) is not heard
        /// this soon after the room-won kill, which says it already: one sound for one moment.
        /// </summary>
        public float WayOpensQuiet = 1.5f;
    }

    /// <summary>Part 3 of work order 080, a clean press is heard and a chain rises. First guesses.</summary>
    [Serializable]
    public sealed class ChainTickValues
    {
        /// <summary>Off: a press makes no sound of its own, and the clean tap's tone plays at its shot as this morning.</summary>
        public bool On = true;
        /// <summary>Semitones each clean press in a row is above the one before (3: a minor third, clear at his tempo).</summary>
        public float StepSemitones = 3f;
        /// <summary>The steps stop rising here (a chain of six with Fourth Tap and Steady Hand stays in a singable range).</summary>
        public int MostSteps = 5;
        /// <summary>A chain of at least this many presses, every one after the first clean, ends on the top note (the game's full clean chain: 3).</summary>
        public int TopFrom = 3;
        /// <summary>
        /// A press that was early, late or waited: false, silent (decided for him: he should miss the sound when he is off);
        /// true, a dull tick.
        /// </summary>
        public bool DullTickWhenOff;
        /// <summary>
        /// The tick replaces the clean tap's tone that this morning rang when its shot went off: one sound for one press.
        /// Its word and its tone's report (for the crit's tick) stay.
        /// </summary>
        public bool TakesTheCleanTone = true;
    }

    /// <summary>
    /// Work order 080 (2026-11-22, "weight in a hit, a kill and a clean press"): its one switch and its four parts' values,
    /// defaults in code. <see cref="On"/> off gives this morning's game. Part 4 (she is hit) had a sound and a mark of its
    /// own already (her hurt crack, louder for a bigger hit, and the hurt spark on her): nothing of it is here.
    /// </summary>
    [Serializable]
    public sealed class CombatWeightValues
    {
        /// <summary>Off: everything of work order 080 off, the morning's game.</summary>
        public bool On = true;
        public HitWeightValues Hits = new HitWeightValues();
        public KillWeightValues Kills = new KillWeightValues();
        public ChainTickValues Chain = new ChainTickValues();
    }

    /// <summary>The rules of work order 080, with no engine in them.</summary>
    public static class CombatWeightRules
    {
        /// <summary>A hit's weight: a crit whatever made it; else a skill's hit (a slot) is heavy, the basic shot's (-1) basic.</summary>
        public static HitWeight WeightOf(int slot, bool crit) => crit ? HitWeight.Crit : slot >= 0 ? HitWeight.Heavy : HitWeight.Basic;

        /// <summary>The impact a weight sounds.</summary>
        public static PlaceholderSound SoundOf(HitWeight weight) =>
            weight == HitWeight.Crit ? PlaceholderSound.HitCrit : weight == HitWeight.Heavy ? PlaceholderSound.HitHeavy : PlaceholderSound.HitBasic;

        /// <summary>The kill's sound: the room won when it emptied its room.</summary>
        public static PlaceholderSound KillSound(bool roomWon) => roomWon ? PlaceholderSound.RoomWon : PlaceholderSound.Kill;

        /// <summary>The sound of a press's tick, or none.</summary>
        public static PlaceholderSound? SoundOf(ChainTickKind kind)
        {
            switch (kind)
            {
                case ChainTickKind.Step: return PlaceholderSound.CleanTick;
                case ChainTickKind.Top: return PlaceholderSound.ChainTop;
                case ChainTickKind.Off: return PlaceholderSound.OffTick;
                default: return null;
            }
        }

        /// <summary>The pitch of step <paramref name="step"/> (from 1, 1 for the first): each a number of semitones above the one before, up to the most.</summary>
        public static float StepPitch(int step, ChainTickValues v)
        {
            v = v ?? new ChainTickValues();
            int s = Math.Max(1, Math.Min(step, Math.Max(1, v.MostSteps)));
            return (float)Math.Pow(2.0, (s - 1) * v.StepSemitones / 12.0);
        }

        /// <summary>The pitch of variation <paramref name="index"/> of a list; 1 for none.</summary>
        public static float PitchOf(float[] pitches, int index) =>
            pitches != null && index >= 0 && index < pitches.Length && pitches[index] > 0f ? pitches[index] : 1f;
    }

    /// <summary>
    /// The variation of a sound (work order 080): an index into its variations, never the one before when there are two or
    /// more, from a seeded source of its own (never Unity's Random, which the game's own draws use).
    /// </summary>
    public sealed class NoRepeat
    {
        uint _state;
        int _last = -1;

        public NoRepeat(uint seed) => _state = seed == 0u ? 0x9E3779B9u : seed;

        /// <summary>The variation played last, or -1.</summary>
        public int Last => _last;

        /// <summary>The next of <paramref name="count"/> variations: 0 for one or none; never the last when there are more.</summary>
        public int Next(int count)
        {
            if (count <= 1) return _last = 0;
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            if (_last < 0 || _last >= count) return _last = (int)(_state % (uint)count);
            int other = (int)(_state % (uint)(count - 1)); // one of the others
            return _last = other >= _last ? other + 1 : other;
        }
    }

    /// <summary>An impact let through at a frame's end (work order 080): its weight, how many hits of the frame it stands for, and whether a clean press made one.</summary>
    public struct HitStart
    {
        public HitWeight Weight;
        public int Hits;
        public bool Clean;
    }

    /// <summary>
    /// The limits on impacts (work order 080): the hits of a frame are gathered and let through at its end, the heaviest
    /// first, at most <see cref="HitWeightValues.PerFrame"/> of them, and at most <see cref="HitWeightValues.PerWindow"/> in
    /// any <see cref="HitWeightValues.Window"/> seconds. A clean press's edge goes with the impact it was gathered into (the
    /// heaviest of the frame stands for all). Time is passed in, in seconds. Nothing is made while it runs.
    /// </summary>
    public sealed class HitGate
    {
        const int Weights = 3;
        readonly int[] _count = new int[Weights];
        bool _clean;
        float[] _started = new float[8];
        int _startedCount;
        readonly List<HitStart> _out = new List<HitStart>(Weights);

        /// <summary>A hit of hers landed this frame.</summary>
        public void Note(HitWeight weight, bool clean)
        {
            int i = (int)weight;
            if (i < 0 || i >= Weights) return;
            _count[i]++;
            _clean |= clean;
        }

        /// <summary>Is anything gathered and not yet let through?</summary>
        public bool Waiting => _count[0] > 0 || _count[1] > 0 || _count[2] > 0;

        /// <summary>The end of a frame at <paramref name="now"/>: what starts. The list is reused by the next call.</summary>
        public IReadOnlyList<HitStart> Flush(float now, HitWeightValues v)
        {
            _out.Clear();
            if (!Waiting) return _out;
            v = v ?? new HitWeightValues();
            Forget(now, v.Window);
            int total = _count[0] + _count[1] + _count[2];
            int others = 0;
            for (int i = Weights - 1; i >= 0; i--) // the heaviest first
            {
                if (_count[i] == 0) continue;
                if (_out.Count >= Math.Max(0, v.PerFrame) || _startedCount >= Math.Max(0, v.PerWindow)) break;
                if (_out.Count > 0) others += _count[i];
                _out.Add(new HitStart { Weight = (HitWeight)i, Hits = _count[i], Clean = _out.Count == 0 && _clean });
                Started(now);
            }
            // The heaviest let through stands for every hit of the frame that is not let through on its own.
            if (_out.Count > 0)
            {
                HitStart first = _out[0];
                first.Hits = total - others;
                _out[0] = first;
            }
            Array.Clear(_count, 0, Weights);
            _clean = false;
            return _out;
        }

        void Forget(float now, float window)
        {
            int kept = 0;
            for (int i = 0; i < _startedCount; i++)
            {
                float t = _started[i];
                if (t > now || now - t >= Math.Max(0f, window) - 1e-4f) continue; // a clock that went back forgets too
                _started[kept++] = t;
            }
            _startedCount = kept;
        }

        void Started(float now)
        {
            if (_startedCount == _started.Length) Array.Resize(ref _started, _started.Length * 2); // only past eight in a window
            _started[_startedCount++] = now;
        }

        /// <summary>Forgets what was gathered and started.</summary>
        public void Clear()
        {
            Array.Clear(_count, 0, Weights);
            _clean = false;
            _startedCount = 0;
        }
    }

    /// <summary>
    /// The clean presses in a row of a recast chain (work order 080, part 3; Kristian of Triple Tap's presses, 2026-11-21:
    /// "veldig utydlig når skal skje"): each press after the first that falls in its clean part sounds a step higher than
    /// the clean one before it; a press off its clean part resets the row (silent, or a dull tick); and the last press of a
    /// chain of at least <see cref="ChainTickValues.TopFrom"/> presses, every one after the first clean, ends on the top
    /// note. A press is told by its slot and its number in the chain (2 on, as the character reports it); a press that is
    /// no next press of the chain heard last starts a new row.
    /// </summary>
    public sealed class CleanRun
    {
        int _slot = -1;
        int _shot;
        int _run;

        /// <summary>The clean presses in a row now.</summary>
        public int Run => _run;

        /// <summary>
        /// A recast press: of <paramref name="slot"/>, press <paramref name="shot"/> of its chain (2 on);
        /// <paramref name="inClean"/>: it fell in its clean part; <paramref name="last"/>: it is the chain's last press.
        /// </summary>
        public ChainTick Press(int slot, int shot, bool inClean, bool last, ChainTickValues v)
        {
            v = v ?? new ChainTickValues();
            bool next = slot == _slot && shot == _shot + 1 && shot > 2;
            if (!next) _run = 0;
            _slot = slot;
            _shot = shot;
            if (!inClean)
            {
                _run = 0;
                return new ChainTick { Kind = v.DullTickWhenOff ? ChainTickKind.Off : ChainTickKind.None };
            }
            _run++;
            bool allClean = _run == shot - 1;
            if (last && allClean && shot >= Math.Max(2, v.TopFrom)) return new ChainTick { Kind = ChainTickKind.Top, Step = _run };
            return new ChainTick { Kind = ChainTickKind.Step, Step = _run };
        }

        /// <summary>Forgets the row.</summary>
        public void Clear()
        {
            _slot = -1;
            _shot = 0;
            _run = 0;
        }
    }
}
