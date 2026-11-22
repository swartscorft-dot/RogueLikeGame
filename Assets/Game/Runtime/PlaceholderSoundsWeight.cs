using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 080 (2026-11-22, Kristian's tick "Lyd og følelse i kamp": more weight in hits, kills and clean presses;
    /// of 2026-11-21: "skal være litt dopamin hit", and of Triple Tap's presses "veldig utydlig når skal skje"): what comes
    /// back to her. Work order 064 gave the shot LEAVING her its weight; this gives the shot LANDING, the enemy DYING and
    /// the press being CLEAN theirs, as sounds by name (<see cref="PlaceholderSound"/>, so that a recording of his replaces
    /// each, as every placeholder) and one small effect, all from what the game reports, changing nothing in a fight.
    ///
    /// - A hit of hers that lands is heard by its weight (<see cref="CombatWeightRules.WeightOf"/>): the basic shot light, a
    ///   skill's hit heavy, a crit a hard crack, in place of the morning's one hit tick and the crit's tick over it; a hit
    ///   from a press in its clean part with a bright edge on top. Gathered through the frame and let through at its end
    ///   (<see cref="HitGate"/>): the heaviest of the frame for them all, a little louder for more, and only so many in a
    ///   short time. Each in a few variations, never the same twice in a row (<see cref="NoRepeat"/>).
    /// - A kill by her hand (the hit that left the enemy dead) has its own sound in place of the enemy's death thump, and a
    ///   small burst where it stood (<see cref="KillBursts"/>, pooled); the kill that empties its room a longer one, and
    ///   the stone shift of the way opening is not heard over it.
    /// - A press of a recast chain in its clean part ticks, a step higher for each clean one in a row; the last of a chain
    ///   clean all through rings the top note (<see cref="CleanRun"/>); a press off its clean part is silent (or a dull tick).
    ///   The tick takes the place of the clean tap's tone at its shot (<see cref="CleanToneTaken"/>).
    ///
    /// <see cref="Weight"/>'s <see cref="CombatWeightValues.On"/> off gives this morning's sounds and no burst; each part has its own switch.
    /// </summary>
    public sealed partial class PlaceholderSounds
    {
        const uint HitSeed = 80u, KillSeed = 81u;
        const int MostSlots = 8;
        const int MostRemembered = 256; // kills remembered until their enemy reports its death; past it, forgotten

        /// <summary>Work order 080's switch and its parts' values, defaults in code (first guesses).</summary>
        public CombatWeightValues Weight = new CombatWeightValues();

        /// <summary>For tests: her kills heard, the room-won ones among them, and the impacts let through.</summary>
        public int KillsHeard { get; private set; }
        public int RoomsWonHeard { get; private set; }
        public int ImpactsHeard { get; private set; }

        /// <summary>The pool of kill bursts, or null before the first kill.</summary>
        public KillBursts Bursts => _bursts;

        /// <summary>
        /// True while a clean press's own tick plays (work order 080, <see cref="ChainTickValues.TakesTheCleanTone"/>): the
        /// timing feedback's clean tone is not played over it (its word, and its report for the crit's tick, stay).
        /// </summary>
        public static bool CleanToneTaken
        {
            get
            {
                PlaceholderSounds s = Current;
                return s != null && s.Values.On && s.Weight != null && s.Weight.On && s.Weight.Chain != null && s.Weight.Chain.On
                       && s.Weight.Chain.TakesTheCleanTone;
            }
        }

        readonly HitGate _hitGate = new HitGate();
        readonly NoRepeat[] _hitVariants = { new NoRepeat(HitSeed), new NoRepeat(HitSeed + 10u), new NoRepeat(HitSeed + 20u) };
        readonly NoRepeat _killVariant = new NoRepeat(KillSeed);
        readonly CleanRun _cleanRun = new CleanRun();
        readonly bool[] _cleanShot = new bool[MostSlots]; // the shot of this slot comes from a press in its clean part
        readonly HashSet<Enemy> _killed = new HashSet<Enemy>(); // her kills whose enemy has not yet reported its death
        KillBursts _bursts;
        int _killsThisFrame;
        bool _roomWonThisFrame;
        float _roomWonAt = float.NegativeInfinity; // real time
        Action _weigh;

        bool WeightOn => Weight != null && Weight.On;

        /// <summary>
        /// One of her reports, for work order 080. True when it took the report's sound: a hit that landed, heard by its
        /// weight at the frame's end, so the morning's hit tick and crit tick are not asked.
        /// </summary>
        bool Weigh(PlayerReport r)
        {
            if (!WeightOn) return false;
            switch (r.Kind)
            {
                case PlayerReportKind.RecastPressed:
                    Pressed(r);
                    return false;
                case PlayerReportKind.SkillFired:
                    // Its hits were reported just before it: the clean press's edge is spent.
                    if (r.Slot >= 0 && r.Slot < MostSlots) _cleanShot[r.Slot] = false;
                    return false;
                case PlayerReportKind.SkillEnded:
                    // A clean press whose shot never went off (a dash) gives no edge to the next.
                    if (r.Ending != SkillEnding.RanOut && r.Slot >= 0 && r.Slot < MostSlots) _cleanShot[r.Slot] = false;
                    return false;
                case PlayerReportKind.Hit:
                    if (r.Damage <= 0) return false;
                    NoteKill(r.Target);
                    HitWeightValues hits = Weight.Hits;
                    if (hits == null || !hits.On) return false;
                    bool clean = hits.CleanEdge && r.Slot >= 0 && r.Slot < MostSlots && _cleanShot[r.Slot];
                    if (Values.On && PlaceholderSoundRules.MayStart(PlaceholderSound.HitBasic, Held))
                        _hitGate.Note(CombatWeightRules.WeightOf(r.Slot, r.Crit), clean);
                    return true;
            }
            return false;
        }

        /// <summary>A press of a recast chain: its tick, by the clean presses in a row before it.</summary>
        void Pressed(PlayerReport r)
        {
            bool inClean = r.Timing == RecastTiming.Clean;
            if (r.Slot >= 0 && r.Slot < MostSlots) _cleanShot[r.Slot] = inClean;
            int count = _player != null ? _player.RecastCount : 0;
            bool last = r.Clean || (count > 0 && r.Shot >= count);
            ChainTick tick = _cleanRun.Press(r.Slot, r.Shot, inClean, last, Weight.Chain);
            if (Weight.Chain == null || !Weight.Chain.On) return;
            PlaceholderSound? sound = CombatWeightRules.SoundOf(tick.Kind);
            if (!sound.HasValue) return;
            float pitch = tick.Kind == ChainTickKind.Step ? CombatWeightRules.StepPitch(tick.Step, Weight.Chain) : 1f;
            AskAt(sound.Value, 1f, pitch, tick.Step, exact: true);
        }

        /// <summary>The hit that left an enemy dead is her kill: heard at the frame's end, its burst now.</summary>
        void NoteKill(Damageable target)
        {
            KillWeightValues kills = Weight.Kills;
            if (kills == null || !kills.On || target == null) return;
            if (!target.TryGetComponent(out Enemy enemy) || !enemy.IsDead) return;
            if (_killed.Count >= MostRemembered) _killed.Clear();
            if (!_killed.Add(enemy)) return;
            bool won = EmptiedItsRoom(enemy);
            KillsHeard++;
            if (won) RoomsWonHeard++;
            if (Values.On && PlaceholderSoundRules.MayStart(PlaceholderSound.Kill, Held))
            {
                _killsThisFrame++;
                _roomWonThisFrame |= won;
            }
            if (won) _roomWonAt = Time.unscaledTime;
            if (kills.Burst && _player != null && _player.ShotFxMaterial != null)
            {
                if (_bursts == null) _bursts = KillBursts.Make(transform);
                _bursts.Burst(enemy.transform.position, won, kills, _player.ShotFxMaterial);
            }
        }

        /// <summary>
        /// True when no enemy of the room it was in is left alive (and none the boss called into it): the floor's rooms
        /// (<see cref="GeneratedLevel.EnemiesByRoom"/>); outside a floor, or in none of its rooms, every enemy there is.
        /// Asked only at a kill.
        /// </summary>
        static bool EmptiedItsRoom(Enemy enemy)
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            if (level != null)
            {
                foreach (KeyValuePair<int, List<Enemy>> room in level.EnemiesByRoom)
                    if (room.Value != null && room.Value.Contains(enemy)) return NoneAlive(level, room.Key);
                foreach (KeyValuePair<int, List<Enemy>> room in level.CalledByRoom)
                    if (room.Value != null && room.Value.Contains(enemy)) return NoneAlive(level, room.Key);
            }
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !all[i].IsDead) return false;
            return true;
        }

        static bool NoneAlive(GeneratedLevel level, int room) =>
            NoneAlive(level.EnemiesByRoom, room) && NoneAlive(level.CalledByRoom, room);

        static bool NoneAlive(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list) || list == null) return true;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) return false;
            return true;
        }

        /// <summary>An enemy reported its death: true when it was her kill, heard already as hers (its death thump is not asked).</summary>
        bool TookDeath(Enemy enemy) => enemy != null && _killed.Remove(enemy);

        /// <summary>The way opening is not heard this soon after the kill that won its room (work order 080: one sound for one moment).</summary>
        bool QuietedByRoomWon(PlaceholderSound sound) =>
            sound == PlaceholderSound.WayOpens && WeightOn && Weight.Kills != null && Weight.Kills.On
            && Time.unscaledTime - _roomWonAt < Weight.Kills.WayOpensQuiet;

        /// <summary>Asks for a sound at a pitch, unless it is off or the game is paused.</summary>
        void AskAt(PlaceholderSound sound, float gain, float pitch, int variant, bool exact)
        {
            if (!Values.On || !PlaceholderSoundRules.MayStart(sound, Held)) return;
            _crowd.Ask(sound, gain, pitch, variant, exact);
        }

        /// <summary>The frame's end, before the crowd is let through: its impacts and its kills, each once.</summary>
        void WeighFrame()
        {
            if (_hitGate.Waiting)
            {
                HitWeightValues hits = Weight.Hits ?? new HitWeightValues();
                IReadOnlyList<HitStart> starts = _hitGate.Flush(Time.unscaledTime, hits);
                for (int i = 0; i < starts.Count; i++)
                {
                    HitStart start = starts[i];
                    NoRepeat variants = _hitVariants[(int)start.Weight];
                    int v = variants.Next(hits.Pitches != null ? hits.Pitches.Length : 0);
                    float gain = PlaceholderSoundRules.Merged(start.Hits, Values.MergeStep, Values.MergeMost);
                    _crowd.Ask(CombatWeightRules.SoundOf(start.Weight), gain, CombatWeightRules.PitchOf(hits.Pitches, v), v + 1, false);
                    if (start.Clean) _crowd.Ask(PlaceholderSound.HitCleanEdge);
                    ImpactsHeard++;
                }
            }
            if (_killsThisFrame > 0)
            {
                KillWeightValues kills = Weight.Kills ?? new KillWeightValues();
                int v = _killVariant.Next(kills.Pitches != null ? kills.Pitches.Length : 0);
                float gain = PlaceholderSoundRules.Merged(_killsThisFrame, Values.MergeStep, Values.MergeMost);
                // The room won stands for every kill of its frame: one sound, not a kill under it.
                _crowd.Ask(CombatWeightRules.KillSound(_roomWonThisFrame), gain, CombatWeightRules.PitchOf(kills.Pitches, v), v + 1, false);
            }
            _killsThisFrame = 0;
            _roomWonThisFrame = false;
        }
    }
}
