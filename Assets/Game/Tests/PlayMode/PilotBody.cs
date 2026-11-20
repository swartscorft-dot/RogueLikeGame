using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// A skill as a pilot knows it (work order 022, 2026-11-20): a copy of the numbers the reward screen and the skill bar
    /// show, and the slot it is in. A copy, so a pilot holds nothing it could change.
    /// </summary>
    public struct PilotSkill
    {
        public int Slot;
        public string Name;
        public SkillInput Input;
        public SkillShape Shape;
        public SkillLock Lock;
        public float Range, HalfWidth, Radius, Angle;
        public int MarksAdded, CleanTapMarks, RecastCount;
        public float MarkDamageBonus, CleanMarkDamageBonus, MarkStaggerBonus;
        public bool OpeningMove;
        public float ChargeTime, PerfectFrom, PerfectTo, HoldMax;
        public float CounterDaze, CounterHalfAngle;
        public float Fuse, MoveDistance;

        /// <summary>A spender: it takes the marks off what it hits, for damage or for stagger.</summary>
        public bool Spends => MarkDamageBonus > 0f || MarkStaggerBonus > 0f;
        public bool HasPerfectBand => PerfectTo > PerfectFrom && PerfectTo > 0f;

        public static PilotSkill Of(SkillDefinition s, int slot) => new PilotSkill
        {
            Slot = slot, Name = s.Name, Input = s.Input, Shape = s.Shape, Lock = s.Lock,
            Range = s.Range, HalfWidth = s.HalfWidth, Radius = s.Radius, Angle = s.Angle,
            MarksAdded = s.MarksAdded, CleanTapMarks = s.CleanTapMarks, RecastCount = s.RecastCount,
            MarkDamageBonus = s.MarkDamageBonus, CleanMarkDamageBonus = s.CleanMarkDamageBonus, MarkStaggerBonus = s.MarkStaggerBonus,
            OpeningMove = s.OpeningMove, ChargeTime = s.ChargeTime, PerfectFrom = s.PerfectFrom, PerfectTo = s.PerfectTo,
            HoldMax = s.HoldMax, CounterDaze = s.CounterDaze, CounterHalfAngle = s.CounterHalfAngle,
            Fuse = s.Fuse, MoveDistance = s.MoveDistance,
        };
    }

    /// <summary>
    /// All a pilot may do with her and know of her (work order 022, 2026-11-20): the intents of
    /// <see cref="PlayerCharacter"/> (Steer, Shoot, DashToward, PressSkill, ReleaseSkill, AimAt, ActivateIdentity, Stop)
    /// and what a player reads off her and the HUD: where she is, her health, her cooldowns and charges, the skill in use,
    /// the recast window, focus, kegs lying. The character and her health are private here; nothing returned is a
    /// reference into the game (skills come as <see cref="PilotSkill"/> copies, places as vectors). This is how the pilots
    /// act only through the intents by construction: a pilot gets this and a <see cref="PilotView"/> of plain values,
    /// and nothing else (<see cref="PilotTests.APilot_HoldsNothingButItsBody_AndPlainValues"/> checks their fields).
    /// It counts the intents it passes on, for the tests.
    /// </summary>
    public sealed class PilotBody
    {
        readonly PlayerCharacter _p;
        readonly PlayerHealth _h;

        public PilotBody(PlayerCharacter player, PlayerHealth health)
        {
            _p = player;
            _h = health;
        }

        // ---------------- What she knows ----------------

        /// <summary>Game time, in seconds: pilots decide on it, never on frames.</summary>
        public float Now => Time.time;
        public Vector3 Position => _p.Position;
        public int Health => _h != null ? _h.Current : 0;
        public int MaxHealth => _h != null ? _h.Max : 0;
        public bool Alive => _h == null || _h.IsAlive;
        public float RunSpeed => _p.Feel.RunSpeed * _p.RunSpeedShare;
        public float DashDistance => _p.Feel.DashDistance;
        public float BasicShotRange => _p.Feel.BasicShotRange;
        public bool IsLocked => _p.IsLocked;
        public bool IsDashing => _p.IsDashing;
        public bool InLightSkill => _p.IsInLightSkill;
        public bool HasPendingShot => _p.HasPendingShot;
        public float DashCooldownRemaining => _p.DashCooldownRemaining;
        public int KeyCount => _p.KeyCount;
        public int ChargesReady(int slot) => _p.SkillChargesReady(slot);
        public float CooldownRemaining(int slot) => _p.SkillCooldownRemaining(slot);
        public float SkillGateRemaining => _p.SkillGateRemaining;
        public int ActiveSkillSlot => _p.ActiveSkillSlot;
        public float SkillChargedSeconds => _p.SkillChargedSeconds;
        public bool InPerfectBand => _p.InPerfectBand;
        public int RecastSlot => _p.RecastSlot;
        public int RecastShots => _p.RecastShots;
        public bool IsRecastWindowOpen => _p.IsRecastWindowOpen;
        public float RecastWindowLeft => _p.RecastWindowLeft;
        public float RecastCleanShare => _p.RecastCleanShare;
        public bool IsFocusFull => _p.IsFocusFull;
        public int KegsLying => _p.KegsLying;
        public Vector3 LastKegPoint => _p.LastKegPoint;
        public bool IsKeyMoving => _p.IsKeyMoving;

        /// <summary>The skill on a key of the bar, as a copy; false for an empty key.</summary>
        public bool TryKey(int key, out PilotSkill skill)
        {
            skill = default;
            int slot = _p.SkillOnKey(key);
            if (slot < 0 || slot >= _p.Skills.Count || _p.Skills[slot] == null) return false;
            skill = PilotSkill.Of(_p.Skills[slot], slot);
            return true;
        }

        /// <summary>The skills on the keys, in key order, as copies.</summary>
        public List<PilotSkill> KeySkills()
        {
            var list = new List<PilotSkill>();
            for (int key = 0; key < KeyCount; key++)
                if (TryKey(key, out PilotSkill s)) list.Add(s);
            return list;
        }

        /// <summary>A slot's skill as a copy, for the skill in use or the recast chain.</summary>
        public bool TrySlot(int slot, out PilotSkill skill)
        {
            skill = default;
            if (slot < 0 || slot >= _p.Skills.Count || _p.Skills[slot] == null) return false;
            skill = PilotSkill.Of(_p.Skills[slot], slot);
            return true;
        }

        // ---------------- The intents, counted ----------------

        public int MovingSteers { get; private set; }
        public int DashPresses { get; private set; }
        public int SkillPresses { get; private set; }
        public int Clicks { get; private set; }

        public void ResetCounts() => MovingSteers = DashPresses = SkillPresses = Clicks = 0;

        public void Steer(Vector3 direction)
        {
            if (new Vector3(direction.x, 0f, direction.z).sqrMagnitude > 1e-6f) MovingSteers++;
            _p.Steer(direction);
        }

        public void Stop() => _p.Stop();

        public void Shoot(Vector3 point)
        {
            Clicks++;
            _p.Shoot(point);
        }

        public void AimAt(Vector3 point) => _p.AimAt(point);

        public IntentResult DashToward(Vector3 point)
        {
            DashPresses++;
            return _p.DashToward(point);
        }

        public IntentResult PressSkill(int slot, Vector3 point)
        {
            SkillPresses++;
            return _p.PressSkill(slot, point);
        }

        public void ReleaseSkill(int slot) => _p.ReleaseSkill(slot);

        public IntentResult ActivateIdentity() => _p.ActivateIdentity();
    }
}
