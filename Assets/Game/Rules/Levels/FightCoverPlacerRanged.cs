using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The fight's cover for work order 124's posts (<see cref="LevelRanged"/>, with <see cref="LevelValues.RangedAroundTheRoom"/> on):
    /// the low cover for the duck serves the near flank's line first (part 3: "It should have a low prop on its line toward the door
    /// where the low-cover rule can give it one"), and each of a crossfire's two posts is given a tall cover on its line toward the
    /// other's side, <see cref="RangedAroundValues.CrossCoverFrom"/> to <see cref="RangedAroundValues.CrossCoverTo"/> in front of it,
    /// where the cover rule can give it (part 8). Nothing is drawn, as for every cover of the props stage (<see cref="PropPlacer"/>);
    /// both come out of the room's own budget of added cover (<see cref="CoverValues.TallAddMost"/>, <see cref="CoverValues.LowAddMost"/>),
    /// and keep every rule an added cover keeps. With the switch off nothing here is asked.
    /// </summary>
    public static partial class PropPlacer
    {
        sealed partial class Room
        {
            /// <summary>
            /// Part 3: a low prop on the line of each near flank shooter that none serves yet (<see cref="Rules.FightCover.LowServes"/>), as
            /// the low rule puts one (<see cref="PutLow"/>) for that shooter alone. Returns how many it added (0 with the switch off).
            /// </summary>
            int RangedNearFlankLow(FightFrame f, PropModel low, List<PropFootprint> runs, LevelValues values, CoverValues cv)
            {
                if (!LevelRanged.On(values) || !values.RangedAround.NearFlank) return 0;
                int added = 0;
                for (int i = 0; i < _room.EnemyGroups.Count && i < _room.EnemyPoints.Count && added < cv.LowAddMost; i++)
                {
                    EnemyGroup g = _room.EnemyGroups[i];
                    if (g?.Ranged == null || g.Ranged.Post != RangedPost.NearFlank || g.Kind != EnemyKind.Shooter) continue;
                    FightFrame one = FrameFor(f, _room.EnemyPoints[i], true);
                    if (Rules.FightCover.LowCount(_level, one, values) > 0) continue;
                    if (!PutLow(one, low, runs, values, cv)) continue;
                    _room.CoverLowAdded++;
                    added++;
                }
                return added;
            }

            /// <summary>The fight's frame with one ranged enemy's place only (a shooter's when <paramref name="shooter"/>).</summary>
            static FightFrame FrameFor(FightFrame f, LevelPoint q, bool shooter)
            {
                var one = new FightFrame { Room = f.Room, Entry = f.Entry, LineDepth = f.LineDepth, Depth = f.Depth, FarDepth = f.FarDepth, LineMiddle = f.LineMiddle };
                one.Ranged.Add(q);
                if (shooter) one.Shooters.Add(q);
                return one;
            }

            /// <summary>
            /// Part 8: for each of a crossfire's two posts with no tall thing on its line toward the other (<see cref="LevelRanged.CrossCovered"/>),
            /// a tall prop of the room's theme on that line, tried from <see cref="RangedAroundValues.CrossCoverFrom"/> to
            /// <see cref="RangedAroundValues.CrossCoverTo"/> in front of it in half metres and up to a metre to either side, while the room
            /// has added fewer than <see cref="CoverValues.TallAddMost"/> tall covers; the first that keeps every rule an added cover keeps.
            /// </summary>
            void RangedCrossfireTall(FightFrame f, PropModel tall, List<PropFootprint> runs, LevelValues values, CoverValues cv)
            {
                if (!LevelRanged.On(values) || !values.RangedAround.Crossfire) return;
                RangedAroundValues r = values.RangedAround;
                var posts = new List<LevelPoint>();
                for (int i = 0; i < _room.EnemyGroups.Count && i < _room.EnemyPoints.Count; i++)
                    if (_room.EnemyGroups[i]?.Ranged?.Post == RangedPost.Crossfire) posts.Add(_room.EnemyPoints[i]);
                if (posts.Count != 2) return;
                for (int k = 0; k < 2; k++)
                {
                    LevelPoint q = posts[k], other = posts[1 - k];
                    if (LevelRanged.CrossCovered(_level, _room, q, other, r)) continue;
                    if (_room.CoverTallAdded >= cv.TallAddMost) return;
                    double dx = other.X - q.X, dy = other.Y - q.Y, l = Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-9) continue;
                    var u = new LevelPoint(dx / l, dy / l);
                    var n = new LevelPoint(-u.Y, u.X);
                    bool done = false;
                    for (double d = r.CrossCoverFrom; d <= r.CrossCoverTo + 1e-9 && !done; d += 0.5)
                        foreach (double off in new[] { 0.0, 0.5, -0.5, 1.0, -1.0 })
                        {
                            var at = new LevelPoint(q.X + u.X * d + n.X * off, q.Y + u.Y * d + n.Y * off);
                            if (!_room.IsOpen((int)Math.Floor(at.X / _cs), (int)Math.Floor(at.Y / _cs))) continue;
                            LevelProp p = Cover(tall, at, 0);
                            if (!Clear(p, runs) || !SouthClear(p.Footprint, cv.SouthClear)) continue;
                            if (PropRules.Fault(_level, _room, p, _room.Props, _v, null, _niches) != null) continue;
                            if (!LevelRanged.CrossCovered(_level, _room, q, other, r, p)) continue;
                            if (!TryAddCover(p)) continue;
                            _room.CoverTallAdded++;
                            done = true;
                            break;
                        }
                }
            }
        }
    }
}
