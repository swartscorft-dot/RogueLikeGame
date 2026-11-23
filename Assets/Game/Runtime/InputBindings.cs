using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Roguelike
{
    /// <summary>Where a binding comes from: a keyboard key or a mouse button.</summary>
    public enum BindingSource { Key, LeftMouse, RightMouse, MiddleMouse }

    /// <summary>
    /// Which way the dash goes (decision 0004, Kristian 2026-11-19): toward the cursor, or the way she moves; with no
    /// move key held, the way she faces.
    /// </summary>
    public enum DashDirection { TowardCursor, TowardMovement }

    /// <summary>One binding: a keyboard key, or a mouse button.</summary>
    [Serializable]
    public struct InputBinding
    {
        public BindingSource Source;
        [Tooltip("The key, when the source is Key.")]
        public Key Key;

        public InputBinding(Key key) { Source = BindingSource.Key; Key = key; }
        public InputBinding(BindingSource mouse) { Source = mouse; Key = Key.None; }

        /// <summary>The button on these devices, or null.</summary>
        public ButtonControl Control(Mouse mouse, Keyboard keyboard)
        {
            switch (Source)
            {
                case BindingSource.LeftMouse: return mouse?.leftButton;
                case BindingSource.RightMouse: return mouse?.rightButton;
                case BindingSource.MiddleMouse: return mouse?.middleButton;
                default: return keyboard != null && Key != Key.None ? keyboard[Key] : null;
            }
        }

        /// <summary>A short label for the skill bar and the help line: "Q", "1", "RMB".</summary>
        public string Label
        {
            get
            {
                switch (Source)
                {
                    case BindingSource.LeftMouse: return "LMB";
                    case BindingSource.RightMouse: return "RMB";
                    case BindingSource.MiddleMouse: return "MMB";
                }
                string name = Key.ToString();
                return name.StartsWith("Digit") ? name.Substring(5) : name;
            }
        }
    }

    /// <summary>
    /// The keys and buttons, in one asset Kristian can edit (decision 0003, Kristian 2026-11-18: "I feel that WASD
    /// movement would be nice. Then Q-1-2-3-4-R-F and right click are abilities."). W A S D move relative to the screen,
    /// the left button shoots at the cursor, Space dashes (toward the cursor, or the way she moves: DashDirection), Z spends the focus gauge,
    /// Left Ctrl held takes cover (Kristian 2026-11-19, an experiment), and one binding
    /// per skill slot, in slot order. The skill bar and the help line show what is here. The builder creates the asset
    /// with these start values when it is missing and never overwrites it.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Input Bindings", fileName = "InputBindings")]
    public sealed class InputBindings : ScriptableObject
    {
        [Header("Movement, relative to the screen")]
        public Key MoveUp = Key.W;
        public Key MoveDown = Key.S;
        public Key MoveLeft = Key.A;
        public Key MoveRight = Key.D;

        [Header("Actions")]
        public InputBinding Shoot = new InputBinding(BindingSource.LeftMouse);
        public Key Dash = Key.Space;
        [Tooltip("Toward the cursor, or the way she moves (the way she faces when no move key is held).")]
        public DashDirection DashDirection = DashDirection.TowardCursor;
        public Key Focus = Key.Z;
        [Tooltip("Opens a chest she stands by (work order 012).")]
        public Key Interact = Key.E;
        [Tooltip("Held: take cover against a wall or prop in reach (Kristian 2026-11-19, an experiment). An asset made before it has no such line and gets Left Ctrl.")]
        public Key TakeCover = Key.LeftCtrl;
        [Tooltip("Held with a move key: sprint, half again her run speed, no shots, over the low props (work order 092, Kristian 2026-11-23). An asset made before it has no such line and gets Left Shift.")]
        public Key Sprint = Key.LeftShift;
        [Tooltip("Opens her sheet, every stat and each skill's numbers; it or Esc closes it (work order 095, Kristian 2026-11-23). An asset made before it has no such line and gets Tab.")]
        public Key Sheet = Key.Tab;

        [Header("Skills, one per slot in order")]
        [Tooltip("Slot 1 is the first skill in Skills.asset (Quick Shot), slot 2 the second, and so on.")]
        public InputBinding[] Skills = DefaultSkills();

        /// <summary>
        /// Q 1 2 3 R 4 F and the right mouse button: Quick Shot, Powder Keg, Fan Fire, Long Shot, Scatter Blast, Heavy Volley,
        /// Backstep, Triple Tap. The four a run starts with sit on Q, R, F and the right button (decision 0005, 2026-11-19).
        /// </summary>
        public static InputBinding[] DefaultSkills() => new[]
        {
            new InputBinding(Key.Q), new InputBinding(Key.Digit1), new InputBinding(Key.Digit2), new InputBinding(Key.Digit3),
            new InputBinding(Key.R), new InputBinding(Key.Digit4), new InputBinding(Key.F), new InputBinding(BindingSource.RightMouse),
        };

        /// <summary>The label of a skill slot's binding, or "" for a slot with none.</summary>
        public string SkillLabel(int slot) => Skills != null && slot >= 0 && slot < Skills.Length ? Skills[slot].Label : "";

        /// <summary>The on-screen help line, from the bindings as they are now.</summary>
        public string HelpLine() => HelpLine(false);

        /// <summary>The help line; with <paramref name="quickDuck"/> (work order 043) the cover key reads as a tap to duck.</summary>
        public string HelpLine(bool quickDuck) => HelpLine(quickDuck, false);

        /// <summary>
        /// The help line; with <paramref name="quickDuck"/> (work order 043) the cover key reads as a tap to duck, and with
        /// <paramref name="duckHeld"/> too (work order 086, the held duck) as held to duck.
        /// </summary>
        public string HelpLine(bool quickDuck, bool duckHeld)
        {
            string Name(Key k) { string n = k.ToString(); return n.StartsWith("Digit") ? n.Substring(5) : n; }
            var skills = new System.Text.StringBuilder();
            if (Skills != null)
                foreach (InputBinding b in Skills) { if (skills.Length > 0) skills.Append(' '); skills.Append(b.Label); }
            // The dash direction as in use: the pause menu's override, if chosen, else this asset's (work order 018).
            return Name(MoveUp) + Name(MoveLeft) + Name(MoveDown) + Name(MoveRight) + " move  |  " + Shoot.Label + " shoot at the cursor  |  "
                 + Name(Dash) + (GameSettings.Dash(DashDirection) == DashDirection.TowardMovement ? " dash the way you move" : " dash to the cursor") + "  |  " + skills + " skills  |  " + Name(Focus) + " focus"
                 + (TakeCover != Key.None ? "  |  " + Name(TakeCover) + (quickDuck ? (duckHeld ? " hold to duck by a low prop" : " tap to duck by a low prop") : " hold to take cover") : "")
                 + (Sprint != Key.None ? "  |  " + Name(Sprint) + " hold to sprint" : ""); // work order 092
        }

        [ContextMenu("Reset to the start values")]
        void ResetToStartValues()
        {
            MoveUp = Key.W; MoveDown = Key.S; MoveLeft = Key.A; MoveRight = Key.D;
            Shoot = new InputBinding(BindingSource.LeftMouse);
            Dash = Key.Space; Focus = Key.Z; Interact = Key.E; TakeCover = Key.LeftCtrl; Sprint = Key.LeftShift; Sheet = Key.Tab;
            DashDirection = DashDirection.TowardCursor;
            Skills = DefaultSkills();
        }
    }
}
