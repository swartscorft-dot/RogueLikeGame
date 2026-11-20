using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The shooter (work order 008): it stands off and shoots, a thin tall yellow capsule. Do you keep moving?
    ///
    /// In the chase it keeps <see cref="ShooterValues.KeepNear"/> to <see cref="ShooterValues.KeepFar"/> from her: it
    /// walks closer when she is farther, backs off when she comes nearer than <see cref="ShooterValues.BackOffUnder"/>
    /// (until she is KeepNear away again, or it has its back to a wall), and otherwise stands. It does not shoot while
    /// it backs off.
    ///
    /// Its one attack is a line aimed at where she stands when the windup begins, shown for the windup and then landing
    /// along its whole length at once; it does not follow her. It needs to see her: it starts no windup while a wall
    /// stands between them (a ray at <see cref="ShooterValues.SightHeight"/> over the ground against cover, the same
    /// cover that stops her shots: the level's walls, the low ones too, and its tall props, so it sees and shoots over
    /// a low prop as she does; Kristian, 2026-11-19), and walks toward her until it does. The line stops at the first
    /// wall on its way, so stepping behind one avoids it. With <see cref="FeelValues.CoverStopsShots"/> off, a wall is
    /// every collider that is not her, an enemy or a target, as before.
    ///
    /// Taking cover (Kristian, 2026-11-19, an experiment): what she is in cover against stops its sight of her and its
    /// shot when it lies between them, a low prop too while she is ducked (<see cref="PlayerCharacter.ShieldsFrom"/>).
    /// It then starts no windup and walks toward her, which may take it round to her side. A windup already going when
    /// she ducks is stopped at the strike, its line and telegraph ending at the thing she is behind. Asked live, so a
    /// shot that winds up while she peeks and strikes after she ducks again is stopped, and one that strikes while she
    /// is up is not.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyShooter : Enemy
    {
        const float CorneredFor = 1.5f; // s it shoots from where it stands when it cannot back off
        static readonly RaycastHit[] s_Hits = new RaycastHit[32];

        public ShooterValues Tuning = new ShooterValues();

        bool _backingOff;
        bool _approaching;
        float _corneredUntil = -1f;
        float _reach;

        public override EnemyKind Kind => EnemyKind.Shooter;
        public override EnemyValues Values => Tuning.Enemy;
        /// <summary>True while it walks away from her to win back its distance.</summary>
        public bool IsBackingOff => _backingOff;
        /// <summary>
        /// How far the line being wound up reaches: its length, or less when a wall cuts it, or what she is in cover
        /// against (an experiment, 2026-11-19), asked now.
        /// </summary>
        public float LineReach => Attack != null ? Reach(Attack) : _reach;

        protected override bool MayAttack(Vector3 toPlayer, float distance)
        {
            float now = Time.time;
            if (distance < Tuning.BackOffUnder && now >= _corneredUntil) _backingOff = true;
            if (distance >= Tuning.KeepNear) _backingOff = false;
            return !_backingOff && SeesHer();
        }

        /// <summary>
        /// Does it see her: no wall between at its sight height (<see cref="Sees"/>), and not covered by what she is in
        /// cover against (taking cover, 2026-11-19, an experiment)?
        /// </summary>
        public bool SeesHer() => Player != null && Sees(Player.Position) && !Player.ShieldsFrom(transform.position);

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            if (_backingOff)
            {
                // Straight away from her, as far as the NavMesh allows; with no room for that it stops and shoots.
                Vector3 away = distance > 1e-3f ? -toPlayer / distance : -transform.forward;
                Vector3 from = transform.position;
                Vector3 to = from + away * (Tuning.KeepNear - distance + 1f);
                if (NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas)) to = hit.position;
                if (Flat(to - from).magnitude < 0.5f)
                {
                    _backingOff = false;
                    _corneredUntil = Time.time + CorneredFor;
                    Halt();
                    return;
                }
                Walk(to);
                return;
            }
            if (distance > Tuning.KeepFar) _approaching = true;
            if (distance <= (Tuning.KeepNear + Tuning.KeepFar) * 0.5f) _approaching = false;
            if (_approaching || !SeesHer()) Walk(Player.Position);
            else Halt();
        }

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            _reach = ClearDistance(AimDirection(), attack.Length);
        }

        /// <summary>How far its line reaches for the walls: its length, or less when a wall cut it at the windup's start.</summary>
        float WallReach(EnemyAttackDefinition attack) => _reach > 0f ? Mathf.Min(_reach, attack.Length) : attack.Length;

        /// <summary>The walls' reach, ended at what she is in cover against while that covers her (an experiment, 2026-11-19).</summary>
        protected override float Reach(EnemyAttackDefinition attack)
        {
            float reach = WallReach(attack);
            if (Player != null && Player.ShieldsFrom(transform.position))
                reach = Player.CoverEntry(Flat(transform.position), AimDirection(), reach);
            return reach;
        }

        /// <summary>The shot would have reached her past the walls, and what she is in cover against covers her from here.</summary>
        protected override bool StoppedByHerCover(EnemyAttackDefinition attack, GroundPoint origin, GroundPoint aim) =>
            Player != null && Player.ShieldsFrom(transform.position)
            && EnemyBrain.Hits(attack, origin, aim, Player.GroundPosition, Placeholders.PlayerRadius, WallReach(attack));

        /// <summary>Does it see that point, with no wall between at its sight height?</summary>
        public bool Sees(Vector3 point)
        {
            Vector3 from = Flat(transform.position) + Vector3.up * Tuning.SightHeight;
            Vector3 to = Flat(point) + Vector3.up * Tuning.SightHeight;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            return length < 1e-3f || ClearDistance(dir / length, length) >= length - 1e-3f;
        }

        /// <summary>
        /// Metres along a flat direction, up to <paramref name="max"/>, before the first wall. With
        /// <see cref="FeelValues.CoverStopsShots"/> on, a wall is cover as her shots take it (<see cref="Cover"/>, Kristian,
        /// 2026-11-19), so it sees and shoots over a low prop as she does; off, it is any collider that is not her, an
        /// enemy or a target, as before.
        /// </summary>
        float ClearDistance(Vector3 flatDir, float max)
        {
            if (Player == null || Player.Feel.CoverStopsShots)
                return Cover.ClearDistance(Flat(transform.position), flatDir, max, Tuning.SightHeight);
            Vector3 from = Flat(transform.position) + Vector3.up * Tuning.SightHeight;
            int n = Physics.RaycastNonAlloc(from, flatDir, s_Hits, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = max;
            for (int i = 0; i < n; i++)
            {
                Collider c = s_Hits[i].collider;
                if (c == null || IsNotAWall(c)) continue;
                if (s_Hits[i].distance < nearest) nearest = s_Hits[i].distance;
            }
            return nearest;
        }

        bool IsNotAWall(Collider c) =>
            (Player != null && c.transform.IsChildOf(Player.transform))
            || c.GetComponentInParent<Enemy>() != null
            || c.GetComponentInParent<Damageable>() != null;
    }
}
