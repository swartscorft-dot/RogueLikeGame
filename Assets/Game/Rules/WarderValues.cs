using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The warder's numbers (work order 079, 2026-11-22: Kristian ticked "Enda en fiendetype", and the producer proposed the
    /// kind for him to correct after he has met it; all first guesses): a slow support that keeps to the back of its pack and
    /// puts a ward on its allies. A warded enemy takes much less damage for as long as the warder lives; kill the warder (or
    /// stagger it) and every ward of its breaks at once and the warded are dazed for a moment. Since work order 070 a room
    /// comes at her all at once, and the question of a fight is "whom do I shoot first": this kind gives it an answer that is
    /// not always "the nearest".
    ///
    /// Decision 0010 (an enemy is read from its body and its telegraph, never from a surprise): a ward is never on before
    /// its raising has shown for <see cref="RaiseSeconds"/> (the warder's body glowing and a thread of light growing from it to
    /// the ally), and while it is on its shell and its thread are seen the whole time (<see cref="WardKeeper"/>).
    /// </summary>
    [Serializable]
    public sealed class WarderValues
    {
        /// <summary>The ward's name, as the log writes it.</summary>
        public const string Ward = "Ward";

        /// <summary>
        /// Its body and its one attack. Health 220: five or six of her basic shots (40 each, Placeholders.BasicShotDamage):
        /// six, five with her flat damage or a crit. Speed 3.3 m/s: two thirds of a swarmer's 5. It wakes and gives up as the
        /// caster does (14 m, 26 m). Its attack (<see cref="Bolt"/>) is the one it throws only alone.
        /// </summary>
        public EnemyValues Enemy = new EnemyValues
        {
            Health = 220, MoveSpeed = 5f * 2f / 3f, AggroRange = 14f, LeashRange = 26f, RespawnDelay = 4f,
            Attacks = new[] { Bolt() },
        };

        /// <summary>
        /// Alone (no ally left alive in its room) it throws the caster's bolt, the bone orb of things that fly (work order
        /// 036: <see cref="CasterValues.Orbs"/>), whole: its name (so its clip, sound and effects are the orb's), band, wind-up,
        /// damage and size; at half the caster's rate, so the recovery makes the cycle twice the caster's (1.0 + 1.3 = 2.3 s;
        /// here 1.0 + 3.6 = 4.6 s). A room must not stall on an enemy that does nothing.
        /// </summary>
        public static EnemyAttackDefinition Bolt()
        {
            EnemyAttackDefinition orb = CasterTactics.Find(new CasterValues().Orbs, CasterValues.BoneOrb);
            return new EnemyAttackDefinition
            {
                Name = orb.Name, Shape = orb.Shape, MinDistance = orb.MinDistance, MaxDistance = orb.MaxDistance,
                Windup = orb.Windup, Recovery = 2f * (orb.Windup + orb.Recovery) - orb.Windup, Damage = orb.Damage, Radius = orb.Radius,
            };
        }

        /// <summary>The caster's numbers for the orb's flight (its speed, size, longest flight), read as the caster reads them.</summary>
        public CasterValues Orbs = new CasterValues();

        // ---------------- The ward (work order 079) ----------------
        public float WardRange = 9f;        // m: an ally without a ward this near it is warded
        public float RaiseSeconds = 1f;     // s of the raising's telegraph (its glow and the growing thread) before the ward is on
        public float WardShare = 0.6f;      // a warded enemy takes this share less damage, from her and from a bomber's blast
        public int MostWards = 2;           // wards of one warder at a time, those being raised counted
        public float WardEvery = 4f;        // s from the start of one raising to the next at the soonest
        public float BreakRange = 14f;      // m: a warded enemy farther than this from it loses its ward (and is dazed)
        public float BreakDaze = 0.6f;      // s the enemy whose ward broke is dazed (the counter's daze: it stands, its wind-up dropped)

        // ---------------- Where it stands (work order 079: the shooter's and the caster's way of keeping its distance) ----------------
        public float KeepNear = 8f;         // m: it backs off to at least this far from her
        public float KeepFar = 10f;         // m: and keeps no farther than this, behind its allies where the room lets it
        public float BackOffUnder = 6f;     // m: it starts to back off when she is nearer than this
        public float BehindBy = 3f;         // m behind the middle of its allies, on the line from her through them
        public float SightHeight = 0.5f;    // m over the ground of the line it sees her along, as the caster's

        /// <summary>
        /// Its stagger bar: staggered (the bar emptied) or stunned it is dazed, and every ward of its breaks. 40 points, two
        /// fifths of a guard's 100; dazed 2.5 s (a guard's 4); its damage while dazed as the guard's.
        /// </summary>
        public StaggerValues Stagger = new StaggerValues { Max = 40f, DazeSeconds = 2.5f, DazedDamageMultiplier = 1.5f };

        // ---------------- Its look (work order 079: the caster's model, tinted the ward's green and shown larger) ----------------
        public float BodyRadius = 0.4f;     // m, the capsule: a caster's 0.35, wider
        public float BodyHeight = 2.1f;     // m: a caster's 1.9, taller
        public float Shown = 1.15f;         // it is shown this times its model's size (the caster's 1.9 m: 2.2 m)
        public float ShellHeight = 1.0f;    // m over the ground of the shell's ring round a warded enemy
        public float ShellGap = 0.3f;       // m between a warded enemy's body and its shell
        public float ShellWidth = 0.08f;    // m, the shell's line
        public float ShellHitWidth = 0.25f; // m, the shell's line as a hit flashes it...
        public float ShellHitSeconds = 0.15f; // ...for this long
        public float ThreadHeight = 1.2f;   // m over the ground of the thread between them
        public float ThreadWidth = 0.06f;   // m, the thread's line
        public float BurstSeconds = 0.3f;   // s the shell takes to burst outward and fade when its ward breaks
        public float BurstGrow = 2.2f;      // times its size the shell bursts out to
        public float GlowPulse = 0.25f;     // s from one glow of its body to the next while it raises a ward

        // ---------------- The warder that reads (work order 107) ----------------
        /// <summary>
        /// Work order 107's switch, on (Kristian ticked "Fiender og boss" on 2026-11-24, whose text named "vokteren som ikke
        /// leses": he had killed warders without noticing what they were). On: its own body (a staff with the ward's green light
        /// over the crowd, a halo, its glows green), the thread from that light and the ring on the warded wider and pulsing, a
        /// warded hit's number marked with a shield, the first warded hit of a run told once, a hum while a ward stands and a
        /// break when it falls (<see cref="WarderReadValues"/>). Off: the warder as before, field for field.
        /// </summary>
        public bool Reads = true;
        /// <summary>The numbers of work order 107 (<see cref="WarderReadRules"/>): first guesses from its pictures.</summary>
        public WarderReadValues Reading = new WarderReadValues();
    }

    /// <summary>Why a ward ended (work order 079), as the log writes it.</summary>
    public enum WardEnd
    {
        /// <summary>The warder died: every ward of its breaks, the warded dazed.</summary>
        WarderKilled,
        /// <summary>The warder was staggered or stunned: every ward of its breaks, the warded dazed.</summary>
        WarderStaggered,
        /// <summary>The warded enemy was farther than <see cref="WarderValues.BreakRange"/> from it: that ward breaks, that one dazed.</summary>
        TooFar,
        /// <summary>The warded enemy died or was taken away: the ward is simply gone.</summary>
        AllyGone,
        /// <summary>The warder gave up and went home, or was taken away: its wards are simply gone.</summary>
        WentHome,
    }

    /// <summary>What happened to one ward in a step (work order 079).</summary>
    public enum WardChangeKind
    {
        /// <summary>A raising began: its telegraph shows.</summary>
        Raising,
        /// <summary>The raising's time is up: the ward is on.</summary>
        On,
        /// <summary>A ward that was on ended (<see cref="WardChange.Dazes"/>: the warded is dazed).</summary>
        Broken,
        /// <summary>A raising ended before the ward was on: no daze.</summary>
        Cancelled,
    }

    public struct WardChange
    {
        public int AllyId;
        public WardChangeKind Kind;
        public WardEnd Why;
        /// <summary>The enemy that lost it is dazed (<see cref="WarderValues.BreakDaze"/>): a ward that was on, broken by the warder's death, its stagger, or the distance.</summary>
        public bool Dazes;
    }

    /// <summary>One ally as a warder sees it in a step (work order 079).</summary>
    public struct WardAlly
    {
        public int Id;
        public GroundPoint At;
        public bool Alive;
        /// <summary>A warder or the act boss: never warded.</summary>
        public bool NeverWarded;
        /// <summary>Another warder's ward is on it or being raised on it: one enemy never carries two.</summary>
        public bool WardedByAnother;
    }

    /// <summary>One ward of a warder: on whom, raised from when, on or still being raised.</summary>
    public sealed class Ward
    {
        public int AllyId;
        public float StartedAt;
        public bool IsOn;
        /// <summary>Damage the ward took off hits on the warded (for the log's "warded away").</summary>
        public int WardedAway;
    }

    /// <summary>
    /// A warder's wards (work order 079), with no engine in it: each step is told the time, where it stands and its allies,
    /// and says what changed. A ward is raised on the nearest ally within <see cref="WarderValues.WardRange"/> that has none
    /// (never itself, another warder or the boss; one enemy never carries two), at most <see cref="WarderValues.MostWards"/> at
    /// a time, no sooner than <see cref="WarderValues.WardEvery"/> after the last raising began; its raising shows for
    /// <see cref="WarderValues.RaiseSeconds"/> and then it is on. A ward breaks when the warded is farther than
    /// <see cref="WarderValues.BreakRange"/> (that one dazed), and every one when the warder is killed or staggered
    /// (<see cref="BreakAll"/>); a raising is cancelled by the same things, with no daze. A ward whose ally died is gone.
    /// </summary>
    public sealed class WardKeeper
    {
        readonly List<Ward> _wards = new List<Ward>(4);
        readonly Stack<Ward> _free = new Stack<Ward>(4);

        /// <summary>Its wards now, being raised and on.</summary>
        public IReadOnlyList<Ward> Wards => _wards;
        /// <summary>Game time the last raising began; negative infinity before the first.</summary>
        public float LastRaisedAt { get; private set; } = float.NegativeInfinity;
        /// <summary>Wards begun since it was made.</summary>
        public int Raised { get; private set; }

        public int OnCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _wards.Count; i++) if (_wards[i].IsOn) n++;
                return n;
            }
        }

        /// <summary>The ward on (or being raised on) this ally, or null.</summary>
        public Ward Of(int allyId)
        {
            for (int i = 0; i < _wards.Count; i++) if (_wards[i].AllyId == allyId) return _wards[i];
            return null;
        }

        /// <summary>Does it hold a ward on (or being raised on) this ally?</summary>
        public bool Holds(int allyId) => Of(allyId) != null;

        /// <summary>
        /// One step: wards whose ally is gone end; those too far break (a raising is cancelled); raisings whose time is up are
        /// on; and when it may, a new raising begins on the nearest ally fit for one. What changed is added to <paramref name="changes"/>.
        /// </summary>
        public void Step(float now, GroundPoint me, IReadOnlyList<WardAlly> allies, WarderValues v, List<WardChange> changes)
        {
            for (int i = _wards.Count - 1; i >= 0; i--)
            {
                Ward w = _wards[i];
                int k = IndexOf(allies, w.AllyId);
                if (k < 0 || !allies[k].Alive) End(i, WardEnd.AllyGone, changes);
                else if (allies[k].At.DistanceTo(me) > v.BreakRange) End(i, WardEnd.TooFar, changes);
            }
            for (int i = 0; i < _wards.Count; i++)
            {
                Ward w = _wards[i];
                if (w.IsOn || now < w.StartedAt + v.RaiseSeconds) continue;
                w.IsOn = true;
                changes?.Add(new WardChange { AllyId = w.AllyId, Kind = WardChangeKind.On });
            }
            if (!MayRaise(now, v)) return;
            int pick = WarderRules.Pick(me, allies, this, v);
            if (pick < 0) return;
            Ward ward = _free.Count > 0 ? _free.Pop() : new Ward();
            ward.AllyId = allies[pick].Id;
            ward.StartedAt = now;
            ward.IsOn = false;
            ward.WardedAway = 0;
            _wards.Add(ward);
            LastRaisedAt = now;
            Raised++;
            changes?.Add(new WardChange { AllyId = ward.AllyId, Kind = WardChangeKind.Raising });
        }

        /// <summary>May a new raising begin now: fewer than the most, and the pause since the last one over?</summary>
        public bool MayRaise(float now, WarderValues v) => _wards.Count < v.MostWards && now >= LastRaisedAt + v.WardEvery;

        /// <summary>Every ward ends at once, for this reason: those on break (dazing when the reason dazes), raisings are cancelled.</summary>
        public void BreakAll(WardEnd why, List<WardChange> changes)
        {
            for (int i = _wards.Count - 1; i >= 0; i--) End(i, why, changes);
        }

        void End(int index, WardEnd why, List<WardChange> changes)
        {
            Ward w = _wards[index];
            _wards.RemoveAt(index);
            changes?.Add(new WardChange
            {
                AllyId = w.AllyId, Kind = w.IsOn ? WardChangeKind.Broken : WardChangeKind.Cancelled, Why = why,
                Dazes = w.IsOn && WarderRules.Dazes(why),
            });
            _free.Push(w);
        }

        static int IndexOf(IReadOnlyList<WardAlly> allies, int id)
        {
            if (allies == null) return -1;
            for (int i = 0; i < allies.Count; i++) if (allies[i].Id == id) return i;
            return -1;
        }
    }

    /// <summary>The warder's rules (work order 079): whom it wards, how much a ward takes off, which ends daze, where it stands.</summary>
    public static class WarderRules
    {
        /// <summary>
        /// The ally a new ward goes on: the nearest that is alive, within <see cref="WarderValues.WardRange"/>, not a warder or the
        /// boss, carrying no ward of this warder or another's. -1 for none.
        /// </summary>
        public static int Pick(GroundPoint me, IReadOnlyList<WardAlly> allies, WardKeeper mine, WarderValues v)
        {
            if (allies == null) return -1;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < allies.Count; i++)
            {
                WardAlly a = allies[i];
                if (!a.Alive || a.NeverWarded || a.WardedByAnother || (mine != null && mine.Holds(a.Id))) continue;
                float d = a.At.DistanceTo(me);
                if (d > v.WardRange || d >= bestDistance) continue;
                best = i;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>Is this kind never warded: a warder, or the act boss?</summary>
        public static bool NeverWarded(EnemyKind kind) => kind == EnemyKind.Warder || kind == EnemyKind.Boss;

        /// <summary>
        /// A hit on a warded enemy: the damage less <paramref name="share"/> of it, rounded to the nearest (halves up), at least
        /// 1 of a hit that did any. One rule for every hit: hers, and a bomber's blast.
        /// </summary>
        public static int Lessened(int damage, float share)
        {
            if (damage <= 0 || share <= 0f) return damage;
            float kept = 1f - Math.Min(1f, share);
            return Math.Max(1, (int)Math.Round(damage * kept, MidpointRounding.AwayFromZero));
        }

        /// <summary>Does a ward broken for this reason daze the enemy that lost it? The warder killed or staggered, or the distance.</summary>
        public static bool Dazes(WardEnd why) => why == WardEnd.WarderKilled || why == WardEnd.WarderStaggered || why == WardEnd.TooFar;

        /// <summary>
        /// Alone: no ally fit for a ward is alive in its room (a warder or the boss does not count: it has nothing to shield).
        /// Then it throws its bolt.
        /// </summary>
        public static bool Alone(IReadOnlyList<WardAlly> allies)
        {
            if (allies == null) return true;
            for (int i = 0; i < allies.Count; i++) if (allies[i].Alive && !allies[i].NeverWarded) return false;
            return true;
        }

        /// <summary>
        /// Where it wants to stand: on the line from her through the middle of its allies, <see cref="WarderValues.BehindBy"/>
        /// past them, but no nearer her than <see cref="WarderValues.KeepNear"/> and no farther than <see cref="WarderValues.KeepFar"/>.
        /// With no allies (or all on her), at <see cref="WarderValues.KeepNear"/> to <see cref="WarderValues.KeepFar"/> on the line
        /// from her through where it stands.
        /// </summary>
        public static GroundPoint Spot(GroundPoint her, GroundPoint me, bool hasAllies, GroundPoint alliesMiddle, WarderValues v)
        {
            GroundPoint through = hasAllies ? alliesMiddle - her : me - her;
            float length = through.Length;
            if (length < 1e-3f)
            {
                through = me - her;
                length = through.Length;
                if (length < 1e-3f) return me;
            }
            GroundPoint dir = through * (1f / length);
            float want = hasAllies ? length + v.BehindBy : me.DistanceTo(her);
            want = Math.Max(v.KeepNear, Math.Min(v.KeepFar, want));
            return her + dir * want;
        }
    }
}
