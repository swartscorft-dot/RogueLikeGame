using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The basic shot of <see cref="PlayerCharacter"/>: the click, the shot's start, what it hits, the pistols' reach and parts,
    /// and the Laser Sight's line (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// How far the basic shot reaches: the feel's, and the run's Longer barrel (work order 025). Work order 060: the feel's
        /// through her pistols' parts first (a sight's metres); the chest's cap stays on the chest's steps only.
        /// </summary>
        public float BasicShotRange =>
            (Modifiers != null ? Modifiers.Weapon.Reach(Feel.BasicShotRange) : Feel.BasicShotRange) + (Modifiers != null ? Modifiers.BasicShotRangeBonus : 0f);

        /// <summary>Half the width of the basic shot's line (work order 060): the feel's, through her pistols' parts in a run.</summary>
        public float BasicShotHalfWidth => Modifiers != null ? Modifiers.Weapon.HalfWidth(Feel.BasicShotHalfWidth) : Feel.BasicShotHalfWidth;

        /// <summary>Her pistols' parts in the run (work order 060), or null outside a run: the stock gun.</summary>
        public AttachmentEffects Weapon => Modifiers != null ? Modifiers.Weapon : null;

        /// <summary>The Laser Sight's line (work order 060); its object exists only once it has shown.</summary>
        public LaserSight Laser => _laser;

        /// <summary>
        /// Press: one basic shot toward a point on the ground, the cursor. It never moves her: standing she shoots
        /// from where she stands, moving she shoots on the move. The shot aims at the live cursor
        /// (<see cref="AimAt"/>) until it goes off. A click while she cannot shoot yet waits for the next shot.
        /// </summary>
        public void Shoot(Vector3 point)
        {
            _shotCursor = point;
            RiseToAct(wouldGoOff: true); // work order 086: down in a held duck, a click brings her up and goes off
            if (!_clicks.Click(Busy, Time.time, _nextShotAt))
            {
                Report(new PlayerReport { Kind = PlayerReportKind.ShotClicked, Result = IntentResult.Buffered });
                return;
            }
            Report(new PlayerReport { Kind = PlayerReportKind.ShotClicked, Result = IntentResult.Started });
            StartBasicShot(Feel, onTheMove: IsMoving);
        }

        /// <summary>Drops a waiting click, as a dash or a skill does, and reports it if one was waiting.</summary>
        void DropWaitingClick()
        {
            if (_clicks.Waiting) Report(new PlayerReport { Kind = PlayerReportKind.ShotDropped });
            _clicks.Clear();
        }

        // ---------------- Shots ----------------

        IntentResult BufferPress(PressKind kind, Vector3 point, int slot = -1)
        {
            _buffer.Store(new Press { Kind = kind, Point = point, Slot = slot, At = Time.time }, Time.time);
            return IntentResult.Buffered;
        }

        /// <summary>
        /// Starts a basic shot toward the cursor. Standing, the character stops; on the move it walks on. Either way
        /// the whole body turns to the aim only if it lies outside the front angle; within it, the upper body aims.
        /// </summary>
        void StartBasicShot(FeelValues feel, bool onTheMove)
        {
            _peekShot = TakePeekShot(feel); // the quick duck's reward (work order 043)
            if (!onTheMove) Halt();
            _aimPoint = AimEnd(_shotCursor);
            _shotTurnsBody = BodyMustTurnToward(Flat(_aimPoint - transform.position), feel);
            // A shot that must turn the body starts the hold: she turns to the aim at the shot turn rate, and the
            // upper body and arms cover what the body has not turned yet. A shot while held renews it.
            _shotFacing.ShotStarted(_shotTurnsBody, Time.time, OffTheMark.ShotTurnHold(feel)); // work order 040
            _actionKind = ActionKind.BasicShot;
            _movingShot = onTheMove;
            if (onTheMove)
            {
                ShotsOnTheMove++;
                StartEasing(feel);
            }
            ActionsStarted++;
            ActionLeadTime = feel.BasicShotStartup;
            _shotStartedAt = Time.time;
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, BasicShotRecoveryNow(feel)); // work order 050: attack speed
            Report(new PlayerReport { Kind = PlayerReportKind.ShotStarted, Moving = onTheMove });
        }

        /// <summary>
        /// The basic shot goes off along its aim: it hits the first living target its line touches
        /// (<see cref="LineShot.FirstHit"/>), or misses. Either way it fires: the streak runs to the target it hit,
        /// or out to the full range. Cover stops the line (Kristian, 2026-11-19): it hits nothing past the first cover
        /// on its centre line, and a shot that hits nothing before it ends there, its streak too, with a hit spark on
        /// the cover. True if it dealt damage.
        /// </summary>
        bool ResolveBasicShot()
        {
            BasicShotsFired++;
            float peekStagger = TakePeekStagger(); // the quick duck's reward (work order 043): 0 for every other basic shot
            Look look = LookFor(null, 0f);
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            _shotWeight = LastShotWeight = ShotWeight.Basic; // work order 064
            PlayBasicShotSound(look); // on a hit or a miss alike; work order 064: never the same twice running

            GroundPoint origin = GroundPosition;
            var aim = new GroundPoint(_aimPoint.x, _aimPoint.z);
            // Work order 060: where it ends is one calculation, the Laser Sight's line's too (BasicShotEnd).
            Damageable hit = BasicShotEnd(origin, aim, out float range, out float stop);
            float halfWidth = BasicShotHalfWidth; // work order 060: a sight's

            Vector3 endPoint = hit != null ? hit.transform.position : _aimPoint;
            bool stopped = hit == null && stop < range;
            if (stopped) endPoint = NoteCoverStop(origin, aim, stop);
            Vector3 end = new Vector3(endPoint.x, muzzle.y, endPoint.z);
            SpawnFlash(look, muzzle, end - muzzle);
            SpawnStreak(look, muzzle, end, 1f);
            if (stopped) SpawnCoverHit(look, end);
            // A lying keg on the shot's way, up to where it ended, goes off at once (work order 015).
            if (_kegs.Count > 0)
            {
                float reach = hit != null ? hit.GroundPosition.DistanceTo(origin) : Mathf.Min(stop, range);
                SetOffKegs(origin, aim, range, halfWidth, reach);
            }
            if (hit == null) return ShotFired(false);
            bool empowered = IsEmpowered;
            // Work order 050: her flat damage. Work order 060: her pistols' parts, a barrel's weight and a grip's cost on the
            // move (the shot was started on the move, or became one before it went off), and a barrel's stagger. Work order
            // 065: her Marked prey, for a target that carries marks.
            int damage = Placeholders.BasicShotDamage + FlatFor(null);
            if (Modifiers != null) damage = Modifiers.Weapon.ShotDamage(damage, _movingShot);
            int applied = Deal(hit, damage, out float focus, peekStagger + (Modifiers != null ? Modifiers.Weapon.ShotStagger : 0f), marked: hit.MarkCount > 0);
            if (applied <= 0) return ShotFired(false);
            ReportHit(-1, hit, applied, empowered, focus);
            SpawnHit(look, hit, muzzle);
            BasicShotHits++;
            // Every few hits, whatever the target, the hit that completes the count marks the target it hit; in the
            // focus window every hit does (decision 0005).
            if (_basicMarks.Hit(Kit.BasicShotsPerMark, empowered)) AddMarks(hit, 1, -1);
            BasicShotHit(hit);
            return ShotFired(true);
        }

        /// <summary>
        /// Where a basic shot from <paramref name="origin"/> toward <paramref name="aim"/> ends now (work order 060: the one
        /// calculation, for the shot and the Laser Sight's line, so the two never disagree): the first living target its line
        /// touches before any cover (returned), or null; its reach (<paramref name="range"/>) and where cover stops its
        /// centre line (<paramref name="stop"/>, the range when nothing does). Nothing is allocated.
        /// </summary>
        Damageable BasicShotEnd(GroundPoint origin, GroundPoint aim, out float range, out float stop)
        {
            IReadOnlyList<Damageable> all = Damageable.All;
            _targetScratch.Clear();
            for (int i = 0; i < all.Count; i++) _targetScratch.Add(all[i].Info);
            range = BasicShotRange; // the run's Longer barrel (work order 025), and a sight's (060)
            stop = CoverRules.LineStop(origin, aim, range, CoverQuery);
            int index = LineShot.FirstHit(origin, aim, range, BasicShotHalfWidth, _targetScratch, stop);
            return index >= 0 ? all[index] : null;
        }

        // ---------------- The Laser Sight's line (work order 060) ----------------

        void LateUpdate() => TickLaser();

        /// <summary>
        /// The Laser Sight's line, once a frame (and by tests): shown in a run with a part fitted that shows it
        /// (<see cref="AttachmentEffects.ShowsLine"/>), while she is alive, not dashing, not stunned, and no screen or choice
        /// holds the game. It runs from her at the height the next shot's streak leaves the gun, along the live cursor as the
        /// shot aims, to where the basic shot would end now (<see cref="BasicShotEnd"/>): the first living target (a brighter
        /// square there), the cover that stops it, or its full reach. It never alternates the hands: the muzzle is only looked at.
        /// </summary>
        public void TickLaser()
        {
            bool show = Modifiers != null && Modifiers.Weapon.ShowsLine && !_dashing && !IsStunned
                        && !PlayerInputDriver.HeldByScreen && !PlayerInputDriver.HeldByChoice;
            if (show && _herHealth == null) _herHealth = GetComponent<PlayerHealth>();
            if (show && _herHealth != null && !_herHealth.IsAlive) show = false;
            if (!show)
            {
                _laser.Hide();
                return;
            }
            GroundPoint origin = GroundPosition;
            float height = ShotHeight;
            Vector3 aimEnd = AimEnd(AtHeight(_shotCursor, height)); // work order 094: under the cursor at the height it is drawn at
            var aim = new GroundPoint(aimEnd.x, aimEnd.z);
            Damageable hit = BasicShotEnd(origin, aim, out float range, out float stop);
            Vector3 end = hit != null ? hit.transform.position : aimEnd;
            if (hit == null && stop < range)
            {
                GroundPoint at = CoverStopPoint(origin, aim, stop);
                end = new Vector3(at.X, 0f, at.Z);
            }
            Vector3 p = transform.position;
            _laser.Show(new Vector3(p.x, height, p.z), new Vector3(end.x, height, end.z), hit, ShotFxMaterial);
        }

        /// <summary>
        /// The one height the shot is drawn and aimed at (work order 094): the next shot's muzzle (<see cref="MuzzleHeightNext"/>).
        /// The Laser Sight's line is drawn at it, the streak leaves the gun at it, and the input driver meets the cursor's ray
        /// with the level plane at it (<see cref="FeelValues.AimAtShotHeight"/>), so the line passes through the cursor on the screen.
        /// </summary>
        public float ShotHeight => MuzzleHeightNext();

        /// <summary>The height the next basic shot's streak leaves the gun at, without taking a turn of the hands (<see cref="NextMuzzle"/>).</summary>
        float MuzzleHeightNext()
        {
            Transform muzzle = _leftHandNext ? LeftMuzzle : RightMuzzle;
            if (muzzle == null) muzzle = _leftHandNext ? RightMuzzle : LeftMuzzle;
            return muzzle != null ? muzzle.position.y : FallbackMuzzleHeight;
        }

        /// <summary>
        /// A basic shot hit (work order 017, the one hook for it): each skill she has with a cut for it takes that off its
        /// cooldown (Quick Draw), and during Slide a skill that says so adds its marks (Slide Shot).
        /// </summary>
        void BasicShotHit(Damageable hit)
        {
            float now = Time.time;
            IReadOnlyList<SkillDefinition> skills = Skills;
            for (int slot = 0; slot < skills.Count; slot++)
                if (skills[slot] != null && skills[slot].BasicHitCooldownCut > 0f && IsEquipped(slot))
                    CooldownFor(slot).CutSeconds(now, skills[slot].BasicHitCooldownCut);
            if (_keyMoving && _keyMoveSlot >= 0 && _keyMoveSlot < skills.Count && skills[_keyMoveSlot].SlideShotMarks > 0)
                AddMarks(hit, skills[_keyMoveSlot].SlideShotMarks, _keyMoveSlot);
        }

        /// <summary>Reports a basic shot that went off, and whether it hit; returns that.</summary>
        bool ShotFired(bool landed)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.ShotFired, Landed = landed });
            return landed;
        }
    }
}
