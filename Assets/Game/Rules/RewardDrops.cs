using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of a reward on the ground and of the guard on every choice (work order 042; Kristian's words of
    /// 2026-11-20: "Several times I insta-pressed an upgrade because I killed the last enemy and the pop-up came at the
    /// same instant. This should happen in the form of a drop on the ground that one picks up with E"). First guesses.
    /// </summary>
    [Serializable]
    public sealed class RewardDropValues
    {
        /// <summary>
        /// The switch: on, what she earns by killing (the elite room's upgrade, the floor's reward, the focus choice when it
        /// comes in the reward's place) lands on the ground and opens only when she picks it up. Off, as before.
        /// </summary>
        public bool On = true;
        /// <summary>Real seconds after any choice opens in which its keys and clicks are ignored; a pick must be a press that went down after them. With the switch off too.</summary>
        public float GuardSeconds = 0.35f;
        /// <summary>Game seconds from the burst to the drop lying still: the arc, then a bounce that settles.</summary>
        public float SettleSeconds = 0.8f;
        /// <summary>Of those, the seconds of the arc through the air.</summary>
        public float ArcSeconds = 0.55f;
        /// <summary>m over the ground it bursts up from, and the top of the arc over the straight line.</summary>
        public float BurstHeight = 1f;
        public float ArcHeight = 2.2f;
        /// <summary>m over the ground it floats at once it lies, and how far it bobs up and down, and how often.</summary>
        public float FloatHeight = 0.75f;
        public float Bob = 0.12f;
        public float BobPerSecond = 0.6f;
        /// <summary>m from where it fell that it lands, the nearest ring tried first; never farther than the last.</summary>
        public float LandDistance = 1.5f;
        public float MostLandDistance = 2.5f;
        /// <summary>m it keeps from a wall or a prop where it lands, so she can stand at it.</summary>
        public float Clearance = 0.5f;
    }

    /// <summary>
    /// The guard on a choice (work order 042): for <see cref="RewardDropValues.GuardSeconds"/> of real time after it opens,
    /// no key or click picks, and a pick is only a press that went down after that. A key that was already down when the
    /// choice opened, or went down while it was guarded, picks nothing however long it is held: it must be let go and
    /// pressed again. Time is passed in, in seconds.
    /// </summary>
    public sealed class ChoiceGuard
    {
        float _openedAt = float.NegativeInfinity;
        float _seconds;

        /// <summary>A choice opened now.</summary>
        public void Open(float now, float seconds)
        {
            _openedAt = now;
            _seconds = Math.Max(0f, seconds);
        }

        /// <summary>The moment from which a press may pick.</summary>
        public float LiveFrom => _openedAt + _seconds;

        /// <summary>True once the guard is over.</summary>
        public bool Live(float now) => now >= LiveFrom;

        /// <summary>How far the guard has gone, 0 when the choice opened, 1 once it is live: the cards light up with it.</summary>
        public float Share(float now) => _seconds <= 0f ? 1f : Math.Max(0f, Math.Min(1f, (now - _openedAt) / _seconds));

        /// <summary>True when a press that went down at <paramref name="pressedAt"/> may pick: it went down once the guard was over.</summary>
        public bool MayPick(float pressedAt) => pressedAt >= LiveFrom;
    }

    /// <summary>Where a drop is, in the air or on the ground, at a moment of its flight (work order 042).</summary>
    public struct DropPose
    {
        /// <summary>The share of the way from where it fell to where it lands, 0 to 1.</summary>
        public float Along;
        /// <summary>m over the ground.</summary>
        public float Height;
        /// <summary>True once it lies still (and floats): it can be picked up.</summary>
        public bool Settled;
        /// <summary>How big it is drawn, 1 its own size: it pulses once it lies.</summary>
        public float Scale;
    }

    /// <summary>
    /// The rules of a reward on the ground (work order 042): where it lands, and how it flies there. No engine: the ground
    /// is asked through the two questions passed in.
    /// </summary>
    public static class RewardDropRules
    {
        const int Angles = 12;

        /// <summary>
        /// Where a drop lands. It starts from where the last enemy fell, or from the room's middle when that place cannot be
        /// stood on. Rings round the start are tried, the nearest first (<see cref="RewardDropValues.LandDistance"/>, then
        /// nearer, then out to <see cref="RewardDropValues.MostLandDistance"/>), each from the way toward the room's middle
        /// round, so it falls inward and not against a wall. A place is taken when it can be stood on and the way to it from
        /// the start is open: never inside a wall or a prop, never behind one. None of them: the start itself, or the middle.
        /// <paramref name="standable"/>: a point she can stand at, clear of walls and props. <paramref name="open"/>: the
        /// straight way between two points can be walked.
        /// </summary>
        public static GroundPoint Landing(GroundPoint fell, GroundPoint middle, Func<GroundPoint, bool> standable,
                                          Func<GroundPoint, GroundPoint, bool> open, RewardDropValues v)
        {
            GroundPoint start = standable(fell) ? fell : middle;
            GroundPoint inward = middle - start;
            float first = inward.Length > 1e-3f ? (float)Math.Atan2(inward.Z, inward.X) : 0f;
            float[] rings = { v.LandDistance, v.LandDistance * 0.6f, (v.LandDistance + v.MostLandDistance) * 0.5f, v.MostLandDistance };
            foreach (float r in rings)
            {
                if (r <= 0f || r > v.MostLandDistance + 1e-4f) continue;
                for (int i = 0; i < Angles; i++)
                {
                    // Round from the inward way, either side in turn: 0, +30, -30, +60, -60 ... degrees.
                    int step = (i + 1) / 2;
                    float angle = first + (i % 2 == 1 ? 1 : -1) * step * (float)(2 * Math.PI / Angles);
                    var at = new GroundPoint(start.X + r * (float)Math.Cos(angle), start.Z + r * (float)Math.Sin(angle));
                    if (standable(at) && open(start, at)) return at;
                }
            }
            return standable(start) ? start : middle;
        }

        /// <summary>
        /// The drop <paramref name="since"/> game seconds after it burst up: an arc from <see cref="RewardDropValues.BurstHeight"/>
        /// over where it fell to the float height over where it lands, a bounce that dies away until it lies still at
        /// <see cref="RewardDropValues.SettleSeconds"/>, then a slow bob and a pulse.
        /// </summary>
        public static DropPose Pose(float since, RewardDropValues v)
        {
            float arc = Math.Max(0.01f, v.ArcSeconds);
            float settle = Math.Max(arc, v.SettleSeconds);
            if (since < arc)
            {
                float s = Math.Max(0f, since) / arc;
                float line = v.BurstHeight + (v.FloatHeight - v.BurstHeight) * s;
                return new DropPose { Along = s, Height = line + 4f * v.ArcHeight * s * (1f - s), Settled = false, Scale = 0.6f + 0.4f * s };
            }
            if (since < settle)
            {
                // One bounce, a third of the arc high, dying away.
                float s = (since - arc) / (settle - arc);
                float bounce = v.ArcHeight / 3f * (float)Math.Sin(Math.PI * s) * (1f - s);
                return new DropPose { Along = 1f, Height = v.FloatHeight + bounce, Settled = false, Scale = 1f };
            }
            float t = since - settle;
            float bob = v.Bob * (float)Math.Sin(2 * Math.PI * v.BobPerSecond * t);
            float pulse = 1f + 0.1f * (float)Math.Sin(2 * Math.PI * v.BobPerSecond * 2f * t);
            return new DropPose { Along = 1f, Height = v.FloatHeight + bob, Settled = true, Scale = pulse };
        }
    }
}
