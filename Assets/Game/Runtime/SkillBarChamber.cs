using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 141: Sixth Chamber's count on the kinetic strip, as part of <see cref="SkillBar"/> (the draft kit-2026-11-26, C2:
    /// "Without a visible count it is a random big hit"; the order: "show the count in the simplest place that is yours"). While she
    /// holds Sixth Chamber with its switch on, a small row of diamonds stands over the strip's left end, one a round of the cylinder
    /// (six, or five from its second tier): the shots fired since the last heavy round in white, the rest faint, and the whole row
    /// gold when the next shot is the heavy round. Nothing else shows it at the sight or on her (the cylinder cue there is work
    /// order 142's). Worked out in <see cref="SkillBar.Tick"/> into numbers and a rectangle, drawn from them on the repaint only:
    /// nothing allocated.
    /// </summary>
    public sealed partial class SkillBar
    {
        /// <summary>Work order 141's switch for this sign only (the upgrade's own switch stays <see cref="BasicShotUpgradeValues.SixthChamber"/>).</summary>
        public static bool ChamberSignOn = true;
        /// <summary>The diamonds' size against the upgrade pips' (<see cref="InterfaceValues.PipSize"/>), and their gap over the strip's plate, in the strip's pixels.</summary>
        public const float ChamberPipScale = 1.6f;
        public const float ChamberGap = 4f;
        /// <summary>The faint diamonds' opacity: the rounds still to fire.</summary>
        public const float ChamberFaint = 0.3f;

        /// <summary>True while the row shows this frame; its rectangle (layout pixels), its rounds, those fired, and whether it is lit gold.</summary>
        public bool ChamberShown { get; private set; }
        public Rect ChamberRow { get; private set; }
        public int ChamberRounds { get; private set; }
        public int ChamberFired { get; private set; }
        public bool ChamberLit { get; private set; }
        float _chamberPip;

        /// <summary>The row's numbers this frame, after the strip's ends are placed.</summary>
        void TickChamber()
        {
            ChamberShown = false;
            if (!ChamberSignOn || Player == null) return;
            int rounds = Player.ChamberRounds;
            if (rounds <= 0) return;
            ChamberRounds = rounds;
            ChamberFired = Mathf.Clamp(Player.ChamberFired, 0, rounds);
            ChamberLit = Player.ChamberNextHeavy;
            float pip = _pipSize * ChamberPipScale + 1f;
            _chamberPip = pip;
            float step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
            float width = rounds * step - (step - pip);
            float x = _left.center.x - width * 0.5f;
            float y = _plate.y - ChamberGap * _strip - pip;
            ChamberRow = new Rect(x, y, width, pip);
            ChamberShown = true;
        }

        void DrawChamber(InterfaceValues v)
        {
            Rect r = ChamberRow;
            float pip = _chamberPip, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
            if (ChamberLit)
            {
                InterfaceStyle.PipRow(r, ChamberRounds, InterfaceStyle.C(v.Gold));
                return;
            }
            int fired = ChamberFired, left = ChamberRounds - fired;
            if (fired > 0) InterfaceStyle.PipRow(new Rect(r.x, r.y, fired * step - (step - pip), pip), fired, InterfaceStyle.C(v.White));
            if (left > 0) InterfaceStyle.PipRow(new Rect(r.x + fired * step, r.y, left * step - (step - pip), pip), left, InterfaceStyle.C(v.White, ChamberFaint));
        }
    }
}
