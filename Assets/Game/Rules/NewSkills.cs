using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The rules of the three skills of work order 109 (2026-11-24: Kristian ticked "Hennes kit", "Flere skills i utvalget ... så
    /// løpene blir mer forskjellige", and said yes by name to the producer's "De tre nye skillsene": "Rikosjett (skuddet hopper til
    /// to fiender til), Røykskudd (fiender i et lite område mister deg av syne et øyeblikk), Deadeye (ett skudd på hver merket
    /// fiende)"), with no engine in them. Their numbers are fields of the skill (<see cref="SkillDefinition.RicochetJumps"/>,
    /// <see cref="SkillDefinition.RicochetReach"/>, <see cref="SkillDefinition.SmokeSeconds"/>, <see cref="SkillDefinition.DeadeyeMost"/>),
    /// their first guesses in <see cref="SkillDefaults.Create"/>. Behind <see cref="RunValues.NewSkills"/>: off, none of the three is
    /// in her list of skills, so none is offered, and the pool is the eight of work order 015.
    /// </summary>
    public static class NewSkillRules
    {
        public const string Ricochet = "Ricochet";
        public const string SmokeRound = "Smoke Round";
        public const string Deadeye = "Deadeye";

        /// <summary>The three, in the order they stand at the end of the skill list.</summary>
        public static readonly string[] Names = { Ricochet, SmokeRound, Deadeye };

        /// <summary>True for one of the three skills of work order 109, by name.</summary>
        public static bool IsNew(string name) => name == Ricochet || name == SmokeRound || name == Deadeye;

        /// <summary>
        /// The skills as the switch has them: with it on the list itself; off, the list without the three (a new array, the
        /// others in their order). A null list is null.
        /// </summary>
        public static SkillDefinition[] Switched(SkillDefinition[] skills, bool on)
        {
            if (skills == null || on) return skills;
            int keep = 0;
            foreach (SkillDefinition s in skills)
                if (s == null || !IsNew(s.Name)) keep++;
            if (keep == skills.Length) return skills;
            var list = new SkillDefinition[keep];
            int i = 0;
            foreach (SkillDefinition s in skills)
                if (s == null || !IsNew(s.Name)) list[i++] = s;
            return list;
        }

        // ---------------- Ricochet ----------------

        /// <summary>
        /// Ricochet's next jump: from target <paramref name="from"/>, the nearest other living target whose centre lies within
        /// <paramref name="reach"/> m of its centre, not one of <paramref name="hit"/> (never the same enemy twice), with a clear
        /// line to it past cover (<see cref="CoverRules.Clear"/>: what cover saves is not hit). Index into
        /// <paramref name="targets"/>, or -1 when none is near. On a tie the first in the list.
        /// </summary>
        public static int NextJump(IReadOnlyList<TargetInfo> targets, int from, IReadOnlyList<int> hit, float reach, CoverLine cover)
        {
            if (targets == null || from < 0 || from >= targets.Count || reach <= 0f) return -1;
            GroundPoint at = targets[from].Position;
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                if (i == from || !targets[i].Alive || Contains(hit, i)) continue;
                float d = at.DistanceTo(targets[i].Position);
                if (d > reach + 1e-4f || d >= bestDistance) continue;
                if (!CoverRules.Clear(at, targets[i].Position, cover)) continue;
                best = i;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>
        /// Every target one Ricochet hits, in order: the first (<paramref name="first"/>, the one its shot along her aim met; -1 a
        /// miss, and nothing more), then up to <paramref name="jumps"/> jumps, each from the last one hit (<see cref="NextJump"/>).
        /// Written into <paramref name="into"/>, which is cleared first: nothing is allocated.
        /// </summary>
        public static void Chain(IReadOnlyList<TargetInfo> targets, int first, int jumps, float reach, CoverLine cover, List<int> into)
        {
            into.Clear();
            if (targets == null || first < 0 || first >= targets.Count || !targets[first].Alive) return;
            into.Add(first);
            for (int j = 0; j < jumps; j++)
            {
                int next = NextJump(targets, into[into.Count - 1], into, reach, cover);
                if (next < 0) return;
                into.Add(next);
            }
        }

        static bool Contains(IReadOnlyList<int> list, int value)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value) return true;
            return false;
        }

        // ---------------- Deadeye ----------------

        /// <summary>
        /// Deadeye's targets: every living target that carries a mark (<paramref name="marks"/>, by the same index), whose near
        /// edge lies within <paramref name="range"/> m of her and to whose centre she has a clear line past cover, nearest first,
        /// at most <paramref name="most"/>. Written into <paramref name="into"/>, cleared first; nothing is allocated. It aims by
        /// itself at marked enemies: that is its job (work order 109, his yes by name).
        /// </summary>
        public static void DeadeyeTargets(IReadOnlyList<TargetInfo> targets, IReadOnlyList<int> marks, GroundPoint her, float range, int most,
                                          CoverLine cover, List<int> into)
        {
            into.Clear();
            if (targets == null || marks == null || most <= 0) return;
            int n = Math.Min(targets.Count, marks.Count);
            for (int i = 0; i < n; i++)
            {
                TargetInfo t = targets[i];
                if (!t.Alive || marks[i] <= 0) continue;
                float d = her.DistanceTo(t.Position);
                if (d - t.Radius > range + 1e-4f) continue;
                if (!CoverRules.Clear(her, t.Position, cover)) continue;
                // Insert in order of distance: a short list, sorted as it is made.
                int at = into.Count;
                while (at > 0 && her.DistanceTo(targets[into[at - 1]].Position) > d) at--;
                if (at >= most) continue;
                into.Insert(at, i);
                if (into.Count > most) into.RemoveAt(into.Count - 1);
            }
        }

        /// <summary>A Deadeye shot's damage on a target it took <paramref name="marks"/> marks off: as a spender's is counted today (<see cref="MarkRules.SpenderDamage"/>).</summary>
        public static int DeadeyeDamage(int damage, int marks, float bonusPerMark) => MarkRules.SpenderDamage(damage, marks, bonusPerMark);

        // ---------------- Smoke Round ----------------

        /// <summary>Is the point inside the cloud (its edge counts)?</summary>
        public static bool Inside(GroundPoint p, SmokeCloud cloud) => p.DistanceTo(cloud.Centre) <= cloud.Radius + 1e-4f;

        /// <summary>Does the straight line from one point to the other pass through the cloud (touch its circle)?</summary>
        public static bool Crosses(GroundPoint from, GroundPoint to, SmokeCloud cloud)
        {
            GroundPoint d = to - from;
            float lengthSq = d.Dot(d);
            float t = lengthSq > 1e-8f ? Clamp01((cloud.Centre - from).Dot(d) / lengthSq) : 0f;
            GroundPoint nearest = from + d * t;
            return nearest.DistanceTo(cloud.Centre) <= cloud.Radius + 1e-4f;
        }

        /// <summary>
        /// Does the smoke hide her from an enemy: it stands in a cloud that is up at <paramref name="now"/>, or its line to her
        /// passes through one (she in a cloud too: the line ends in it)? None of the clouds up, false.
        /// </summary>
        public static bool Hides(IReadOnlyList<SmokeCloud> clouds, int count, GroundPoint enemy, GroundPoint her, float now)
        {
            if (clouds == null) return false;
            int n = Math.Min(count, clouds.Count);
            for (int i = 0; i < n; i++)
            {
                SmokeCloud c = clouds[i];
                if (!c.IsUp(now)) continue;
                if (Inside(enemy, c) || Crosses(enemy, her, c)) return true;
            }
            return false;
        }

        /// <summary>
        /// May a melee enemy begin an attack (work order 109, the smoke): always when the smoke does not hide her from it; hidden,
        /// only with her within <paramref name="reach"/> m (<see cref="SmokeValues.MeleeReach"/>): it still comes at her, and strikes
        /// at what it runs into.
        /// </summary>
        public static bool MeleeMayAttack(bool hidden, float distance, float reach) => !hidden || distance <= reach + 1e-4f;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>One cloud of Smoke Round (work order 109): where it stands, how wide, and until when, in the game's seconds.</summary>
    public struct SmokeCloud
    {
        public GroundPoint Centre;
        public float Radius;
        public float From;
        public float Until;

        public SmokeCloud(GroundPoint centre, float radius, float from, float until)
        {
            Centre = centre;
            Radius = Math.Max(0f, radius);
            From = from;
            Until = until;
        }

        /// <summary>True while it stands: from its landing until its time is out.</summary>
        public bool IsUp(float now) => Radius > 0f && now >= From && now < Until;
    }

    /// <summary>The numbers of the smoke that are not the skill's own (work order 109), first guesses with their defaults in code.</summary>
    public static class SmokeValues
    {
        /// <summary>
        /// m: a melee enemy the smoke hides her from begins an attack only with her this near (work order 109: "melee enemies in it
        /// still come at her but begin no attack until they are out or she is within their reach"). 3 m is the brute's Slam's reach,
        /// the widest of the close attacks; a guess, one number for the three kinds.
        /// </summary>
        public const float MeleeReach = 3f;
        /// <summary>At most this many clouds stand at once (two charges with Twin Smoke, and room over); a ninth takes the oldest's place.</summary>
        public const int MostClouds = 8;
    }
}
