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
        public float BasicShotRange = 10f;        // m, how far a basic shot toward the cursor reaches (decision 0002)
        public float BasicShotHalfWidth = 0.5f;   // m, half the width of its line: it hits the first target the line touches
        public bool AimAtShotHeight = true;       // work order 094 (Kristian 2026-11-23, the Laser Sight off the mouse pointing east or west): the cursor's ray meets the level at the gun's height, so the drawn line and the shot pass through the cursor on the screen; what is placed on the floor keeps the ground. Off: the ground for all, as before

        // Cover
        public bool CoverStopsShots = true;       // walls and tall props stop her shots and skills, and the shooter's line (Kristian 2026-11-19); off, her shots pass through as before

        // Taking cover (Kristian 2026-11-19, an experiment: hold Left Ctrl to lean into a wall or prop and duck shots from afar; first guesses)
        public bool TakeCover = true;             // the switch; off, the key does nothing
        public float CoverReach = 1.5f;           // m from her to the edge of a wall or a prop that blocks walking, within which the key takes cover there
        public float CoverSnapTime = 0.15f;       // s, at most, to move to stand against it
        public float CoverStandOff = 0.6f;        // m from its edge to her middle where she stands against it
        public float CoverMoveShare = 0.4f;       // share of run speed she slides along it at
        public float CoverSlideAngle = 60f;       // degrees: a move key further off its face than this does nothing while the key is held
        public float CoverPeekTime = 0.25f;       // s she stays up after a shot or a skill from cover before she ducks again

        // The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043; first guesses).
        // In place of taking cover while it is on: a tap of the cover key beside a low prop drops her where she stands for a
        // moment (QuickDuck). Not in the feel spec's table nor in the settings asset: these defaults are what the game reads.
        public bool QuickDuck = true;             // the switch; off, the cover key takes cover as above, exactly as before
        public float DuckReach = 1f;              // m from her middle to the edge of a low prop, within which a tap ducks her
        public float DuckSeconds = 0.5f;          // s she stays down; she rises by herself
        public float DuckWait = 1f;               // s from the moment she is up again until the key works again
        public bool DuckPeekShot = true;          // her first basic shot soon after a duck that stopped something staggers as Scatter Blast's hit does
        public float DuckPeekWindow = 0.4f;       // s after she rose in which that shot must start
        // The held duck (work order 086; Kristian 2026-11-22: "vil at den skal være hold for duck. Så kan man gjøre det så lenge
        // man vil"; HeldDuckRules). With it on, DuckSeconds and DuckWait above are the tap's and are not read.
        public bool DuckHold = true;              // the switch: down while the key is held, up at once when it goes up; off, the tap above as before
        public float DuckHoldWait = 0.2f;         // s from the moment she is up again until a press can put her down again (the producer's proposal: short, so it cannot be flickered for free)
        public bool DuckHoldDownAgain = true;     // the key still held after her own shot or skill brought her up: down again once she is free and the wait is over

        // Aiming and shooting on the move (basic shot only)
        public float ShotFrontAngle = 120f;       // degrees to each side of the facing: an aim within it turns only the upper body (Kristian 2026-11-18: 120 in play, works fine)
        public float ShotTurnTime = 0.2f;         // s for a half turn of the body to a shot aimed outside the front angle, and back to the run after
        public float ShotTurnHold = 0.5f;         // s she stays turned to the aim after the last such shot, backpedalling
        public float BackpedalSpeedShare = 0.5f;  // share of run speed she may move at while held turned away from her movement (0003, a guess)
        public float MovingShotSpeedShare = 0.5f; // share of run speed a shot on the move slows to
        public float MovingShotSlowdown = 0.15f;  // s to ease down to that share when a shot on the move starts
        public float MovingShotRecovery = 0.5f;   // s to ease back to full speed, once the shot has gone off and the slow-down is done
        public float ShootGaitFade = 0.15f;       // s to cross-fade between the run and the shooting gait

        // Skills on the move (decision 0004, the producer's guesses, on trial)
        public bool SkillRecoveryFree = true;     // after a skill's last hit the basic shot and the move keys are free; the next skill still waits out the recovery
        public float SkillSlowdown = 0.3f;        // s to slide from full run speed to a stand when a skill is pressed on the move (less from a lower speed); 0 is the old dead stop
        public bool SlideSteering = true;         // the move keys turn a skill's slide the way they hold, at the speed the slide has (Kristian 2026-11-19); off, it keeps the way she ran
        public float SkillSpeedRecovery = 0.25f;  // s to ease back to full speed from a stand when a skill lets her go (less from a higher speed)
        public float AimHold = 0.3f;              // s the arms stay aimed at the cursor after a shot or a skill ends

        // Hits
        public float HitStop = 0.05f;             // s, a skill hit
        public float BasicShotHitStop = 0.025f;   // s, a basic shot hit (halved 2026-11-18 with the faster shot)

        // Quicker off the mark after shooting (work order 040, Kristian's second session of 2026-11-20: "the character can
        // accelerate a little faster still after one has stopped shooting and is to run again"; OffTheMark). Each number
        // stands in for the one named beside it while the switch is on, about a third quicker from the last shot to 90 % of
        // run speed; nothing of how she moves while a shot is going off, her top speed or the dash changes. Not in the
        // feel spec's table nor in the settings asset: these defaults are what the game reads.
        public bool QuickOffTheMark = true;            // off: the numbers they stand in for, as before
        public float MovingShotRecoveryQuick = 0.12f;  // s, for MovingShotRecovery (0.25 in the settings asset): the ease back to full speed after a shot on the move, and out of the backpedal cap
        public float ShotTurnHoldQuick = 0.40f;        // s, for ShotTurnHold (0.5): turned to the aim, backpedalling at the cap, this long after the last such shot started
        public float ChainSpeedRecoveryQuick = 0.165f; // s, for SkillSpeedRecovery (0.25), after a recast chain's shot only (Backstep's): the ease back to full speed from a stand
    }
}
