using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The skills she has and the keys they sit on (work order 015, 2026-11-20: eight skills in a pool, four taken, so no
    /// two runs need end with the same eight). A skill is its place in the skill list; a key is its place in the bindings'
    /// skill keys. There are <see cref="KeyCount"/> keys. A run starts with the start skills on the keys of their own
    /// places in the list (as before the pool: Q, R, F and the right button); a skill taken later goes on the first free
    /// key in key order and stays there for the run. A skill can be had with no key (tests and tools that hand out every
    /// skill): it can be used through the intents, but no key fires it.
    /// </summary>
    public sealed class SkillLoadout
    {
        public const int KeyCount = 8;

        readonly int[] _onKey = new int[KeyCount];
        readonly List<bool> _has = new List<bool>();

        public SkillLoadout() => Clear();

        /// <summary>Nothing had and every key empty.</summary>
        public void Clear()
        {
            for (int k = 0; k < KeyCount; k++) _onKey[k] = -1;
            _has.Clear();
        }

        /// <summary>
        /// A run's start: the start skills, each on the key of its own place when that is a key and free, else on the first
        /// free key. With <paramref name="all"/> every other skill is then taken too, in list order.
        /// </summary>
        public void Start(IReadOnlyList<SkillDefinition> skills, bool all)
        {
            Clear();
            int count = skills?.Count ?? 0;
            for (int i = 0; i < count; i++) _has.Add(false);
            for (int i = 0; i < count; i++)
            {
                if (skills[i] == null || !skills[i].StartEquipped) continue;
                _has[i] = true;
                if (i < KeyCount && _onKey[i] < 0) _onKey[i] = i;
            }
            for (int i = 0; i < count; i++)
                if (_has[i] && KeyOf(i) < 0) Place(i);
            if (all)
                for (int i = 0; i < count; i++) Take(i);
        }

        /// <summary>How many skills it knows of.</summary>
        public int SkillCount => _has.Count;

        public bool Has(int skill) => skill >= 0 && skill < _has.Count && _has[skill];

        /// <summary>The skill on a key, or -1 for an empty key or no such key.</summary>
        public int SkillOn(int key) => key >= 0 && key < KeyCount ? _onKey[key] : -1;

        /// <summary>The key a skill sits on, or -1.</summary>
        public int KeyOf(int skill)
        {
            if (skill < 0) return -1;
            for (int k = 0; k < KeyCount; k++)
                if (_onKey[k] == skill) return k;
            return -1;
        }

        /// <summary>The first key in key order with no skill on it, or -1 when all are taken.</summary>
        public int FirstFreeKey
        {
            get
            {
                for (int k = 0; k < KeyCount; k++)
                    if (_onKey[k] < 0) return k;
                return -1;
            }
        }

        /// <summary>
        /// She has the skill from now on, on the first free key if it is on none yet. Returns its key, or -1 when no key
        /// was free (she still has it). A skill out of the list's range is ignored.
        /// </summary>
        public int Take(int skill)
        {
            if (skill < 0) return -1;
            while (_has.Count <= skill) _has.Add(false);
            _has[skill] = true;
            int key = KeyOf(skill);
            return key >= 0 ? key : Place(skill);
        }

        int Place(int skill)
        {
            int key = FirstFreeKey;
            if (key >= 0) _onKey[key] = skill;
            return key;
        }

        /// <summary>For each skill of the list, whether she has it: what the floor reward draws from.</summary>
        public List<bool> HasList(int count)
        {
            var list = new List<bool>(count);
            for (int i = 0; i < count; i++) list.Add(Has(i));
            return list;
        }
    }

    /// <summary>The rules of the pool's new skills (work order 015, 2026-11-20), with no engine in them.</summary>
    public static class PoolRules
    {
        /// <summary>
        /// Long Shot's perfect band: is a charge of this many seconds, let go by the key, inside it? From PerfectFrom to
        /// PerfectTo, both edges inside. A skill with no band (PerfectTo not above PerfectFrom) is never perfect.
        /// </summary>
        public static bool IsPerfect(SkillDefinition skill, float chargedSeconds)
        {
            if (skill == null || skill.Input != SkillInput.Charge || !(skill.PerfectTo > skill.PerfectFrom)) return false;
            const float edge = 1e-4f;
            return chargedSeconds >= skill.PerfectFrom - edge && chargedSeconds <= skill.PerfectTo + edge;
        }

        /// <summary>A perfect release's damage: the full-charge damage times the perfect scale.</summary>
        public static int PerfectDamage(SkillDefinition skill) =>
            (int)Math.Round(SkillRules.Damage(skill, 1f) * Math.Max(0f, skill.PerfectScale));

        /// <summary>
        /// Breaker Slug: the stagger of a hit that took <paramref name="marksTaken"/> marks, <paramref name="bonusPerMark"/>
        /// more per mark (0.3: +30 % per mark).
        /// </summary>
        public static float SpenderStagger(float stagger, int marksTaken, float bonusPerMark) =>
            stagger * (1f + Math.Max(0f, bonusPerMark) * Math.Max(0, marksTaken));

        /// <summary>
        /// The counter's front (Stopping Shot): is she within <paramref name="halfAngle"/> degrees either side of where the
        /// enemy faces? Standing on it counts as in front.
        /// </summary>
        public static bool InFront(GroundPoint enemy, GroundPoint facing, GroundPoint her, float halfAngle)
        {
            GroundPoint to = her - enemy;
            float distance = to.Length, along = facing.Length;
            if (distance < 1e-4f) return true;
            if (along < 1e-6f) return false;
            double cos = to.Dot(facing) / (distance * along);
            double off = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI;
            return off <= halfAngle + 1e-3;
        }

        /// <summary>
        /// The enemy attacks that are counterable whatever their data says: those the order named (the brute's Shockwave
        /// and the guard's Sweep). The practice brute's attacks live in the enemy settings asset, written before
        /// <see cref="EnemyAttackDefinition.Counterable"/> was there, and that asset is not edited by hand; this keeps
        /// its Shockwave counterable until it is saved with the field. Other attacks say so by their data.
        /// </summary>
        public static bool IsCounterable(EnemyAttackDefinition attack) =>
            attack != null && (attack.Counterable || attack.Name == "Shockwave" || attack.Name == "Sweep");
    }

    /// <summary>
    /// A tag on a target (work order 015, Tag Round): while it lasts the target takes a share more damage from everything
    /// she does. A new tag starts it again with the new numbers; tags do not stack. Time is passed in, in seconds.
    /// </summary>
    public sealed class Tag
    {
        float _until = float.NegativeInfinity;
        float _share;

        public bool IsOn(float now) => now < _until;
        public float Remaining(float now) => now < _until ? _until - now : 0f;

        public void Apply(float now, float seconds, float share)
        {
            _until = now + Math.Max(0f, seconds);
            _share = Math.Max(0f, share);
        }

        public void Clear() => _until = float.NegativeInfinity;

        /// <summary>The damage of one of her hits on it now: raised by the share while it is tagged, rounded.</summary>
        public int Scale(int damage, float now) =>
            damage > 0 && IsOn(now) ? (int)Math.Round(damage * (1f + _share), MidpointRounding.AwayFromZero) : damage;
    }
}
