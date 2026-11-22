using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// What <see cref="PlayerCharacter"/> tells the play session recorder: the event and the reports of a skill's end and a hit
    /// (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// Tells what she did as it happens: what became of each press, each skill and shot that started, went off or
        /// ended, each hit. For the play session recorder (<see cref="PlaySessionRecorder"/>); nothing in the game listens,
        /// and it changes nothing.
        /// </summary>
        public event System.Action<PlayerReport> Reported;

        void Report(PlayerReport report) => Reported?.Invoke(report);

        /// <summary>Reports that the skill in use ends now, and how. Called before the skill is cancelled or let go.</summary>
        void ReportSkillEnded(SkillEnding ending, float now)
        {
            if (Reported == null || _cast.Skill == null) return;
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.SkillEnded, Slot = _castSlot, Ending = ending,
                Held = _cast.HeldSeconds(now), Charge = _cast.Charge(now), Fired = _cast.HitsTaken > 0 && !_castOpening,
            });
        }

        /// <summary>Reports damage she dealt: by a skill's slot, or -1 for the basic shot; and whether the hit countered (work order 019).</summary>
        void ReportHit(int slot, Damageable target, int damage, bool empowered, float focus, bool countered = false) =>
            Report(new PlayerReport
            {
                // Work order 071: a hit in a Hair Trigger part's gains counts as empowered too, so the log's "damage while
                // empowered" is what the gains dealt in every way of the gauge.
                Kind = PlayerReportKind.Hit, Slot = slot, Target = target, Damage = damage, Empowered = empowered || IsFocusBoosted, Focus = focus, Countered = countered,
                Crit = _dealtCrit, // work order 050: of the Deal just before
            });
    }
}
