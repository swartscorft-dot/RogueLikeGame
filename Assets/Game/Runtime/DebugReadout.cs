using System.Globalization;
using System.Text;
using UnityEngine;

namespace Roguelike
{
    /// <summary>A text readout in the top-left corner, for looking at what the character is doing.</summary>
    public sealed class DebugReadout : MonoBehaviour
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        GUIStyle _style;
        readonly StringBuilder _text = new StringBuilder();

        void OnGUI()
        {
            if (!DeveloperKeys.On) return; // work order 066: the developer's text, never shown while his keys are off
            // Work order 081: a box with no button takes nothing from the layout or input events: its text is made once a frame,
            // for the repaint, not for every event of the frame.
            if (Event.current.type != EventType.Repaint) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerCharacter>();
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft };

            // Laid out for a view 720 pixels high and scaled to the real height, like the skill bar.
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            _text.Clear();
            if (_driver == null && _player != null) _driver = _player.GetComponent<PlayerInputDriver>();
            _text.AppendLine(_driver != null ? _driver.ActiveBindings.HelpLine(_player != null && _player.QuickDuckOn, _player != null && _player.DuckIsHeld) : ""); // work order 043; 086: held
            int lines = 2;
            if (_player != null)
            {
                Vector3 p = _player.Position;
                _text.AppendLine(string.Format(s_Inv, "pos ({0:0.00}, {1:0.00})  speed {2:0.0} m/s", p.x, p.z, _player.Speed));
                Vector3 m = _player.MoveInput;
                _text.AppendLine(string.Format(s_Inv, "move ({0:0.0}, {1:0.0}){2}{3}", m.x, m.z, _player.IsMoving ? "" : "  standing",
                    _player.HasPendingShot ? "  click waiting" : ""));
                int recovering = _player.FreeRecoverySlot;
                _text.AppendLine(string.Format(s_Inv, "action {0}{1} {2}{3}{4}{5}{6}", _player.Action,
                    _player.ActiveSkill != null ? " (" + _player.ActiveSkill.Name + ")" : "", _player.ActionPhase,
                    _player.IsSkillSliding ? "  sliding" : "",
                    recovering >= 0 ? string.Format(s_Inv, "  {0} recovery (free) {1:0.00} s", _player.Skills[recovering].Name, _player.SkillGateRemaining) : "",
                    _player.HasBufferedPress ? "  buffered press" : "", HitStop.IsActive ? "  HIT-STOP" : ""));
                _text.AppendLine(string.Format(s_Inv, "dash cd {0:0.0} s{1}{2}", _player.DashCooldownRemaining,
                    _player.IsDashing ? "  DASHING" : "", _player.IsInvulnerable ? "  i-frame" : ""));
                _text.AppendLine(string.Format(s_Inv, "basic {0}/{1} hit  skill hits {2}, targets hit {3}",
                    _player.BasicShotHits, _player.BasicShotsFired, _player.SkillShotsFired, _player.SkillShotHits));
                lines += 5;
            }
            foreach (Damageable d in Damageable.All)
            {
                _text.AppendLine(string.Format(s_Inv, "{0} {1}/{2}", d.name, d.CurrentHealth, d.MaxHealth));
                lines++;
            }
            _text.Append(string.Format(s_Inv, "frame {0}", Time.frameCount));

            GUI.Box(new Rect(10, 10, 540, 10 + lines * 15), _text.ToString(), _style);
            GUI.matrix = matrixBefore;
        }
    }
}
