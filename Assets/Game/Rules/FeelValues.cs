using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The feel-spec values the game uses, with the start values from docs/vision/feel-spec.md as defaults.
    /// One instance lives in the FeelSettings asset, where Kristian tunes it. A rule test checks the
    /// defaults against the table, so a change here must be a change there too.
    /// </summary>
    [Serializable]
    public sealed class FeelValues
    {
        // Camera
        public float CameraPitch = 55f;           // degrees from the ground
        public float CameraDistance = 28f;        // m
        public float FieldOfView = 30f;           // degrees

        // Movement
        public float RunSpeed = 6f;               // m/s
        public float TimeToFullSpeed = 0f;        // s
        public float TurnTime = 0f;               // s for a half turn

        // Dash
        public float DashCooldown = 10f;          // s
        public float DashDistance = 6f;           // m
        public float DashDuration = 0.2f;         // s
        public float DashInvulnerability = 0.1f;  // s
        public bool DashCancelsSkill = true;

        // Casting
        public bool FaceCursorOnCast = true;
        public float InputBuffer = 0.15f;         // s

        // Basic shot
        public float BasicShotStartup = 0.08f;    // s (halved 2026-11-18: shoot more often)
        public float BasicShotActive = 0.02f;     // s
        public float BasicShotRecovery = 0.125f;  // s
        public float BasicAttackRange = 6f;       // m
        public float AttackMoveClickRadius = 2f;  // m, from the click to a target's edge

        // Aiming and shooting on the move (basic shot only)
        public float ShotFrontAngle = 60f;        // degrees to each side of the facing: a target within it turns only the upper body
        public float MovingShotSpeedShare = 0.5f; // share of run speed a shot on the move slows to
        public float MovingShotSlowdown = 0.15f;  // s to ease down to that share when a shot on the move starts
        public float MovingShotRecovery = 0.5f;   // s to ease back to full speed, once the shot has gone off and the slow-down is done
        public float ShootGaitFade = 0.15f;       // s to cross-fade between the run and the shooting gait

        // Hits
        public float HitStop = 0.05f;             // s, a skill hit
        public float BasicShotHitStop = 0.025f;   // s, a basic shot hit (halved 2026-11-18 with the faster shot)
    }
}
