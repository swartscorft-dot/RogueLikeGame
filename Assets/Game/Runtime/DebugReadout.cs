using System.Globalization;
using System.Text;
using UnityEngine;

namespace Roguelike
{
    /// <summary>A text readout in the top-left corner. The only UI in G2.</summary>
    public sealed class DebugReadout : MonoBehaviour
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        GUIStyle _style;
        readonly StringBuilder _text = new StringBuilder();

        void OnGUI()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerCharacter>();
            if (_driver == null) _driver = FindAnyObjectByType<PlayerInputDriver>();
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft };

            _text.Clear();
            _text.AppendLine("RMB move  |  A then LMB attack-move  |  Space dash  |  Q skill");
            int lines = 2;
            if (_player != null)
            {
                Vector3 p = _player.Position;
                _text.AppendLine(string.Format(s_Inv, "pos ({0:0.00}, {1:0.00})  speed {2:0.0} m/s", p.x, p.z, _player.Speed));
                Damageable goesFor = _player.OrderTarget;
                _text.AppendLine(string.Format(s_Inv, "order {0}{1}{2}", _player.Order,
                    goesFor != null ? " -> " + goesFor.name : "",
                    _driver != null && _driver.IsAimingAttackMove ? "  AIMING attack-move" : ""));
                _text.AppendLine(string.Format(s_Inv, "action {0} {1}{2}{3}", _player.Action, _player.ActionPhase,
                    _player.HasBufferedPress ? "  buffered press" : "", HitStop.IsActive ? "  HIT-STOP" : ""));
                _text.AppendLine(string.Format(s_Inv, "dash cd {0:0.0} s{1}{2}", _player.DashCooldownRemaining,
                    _player.IsDashing ? "  DASHING" : "", _player.IsInvulnerable ? "  i-frame" : ""));
                _text.AppendLine(string.Format(s_Inv, "basic {0}/{1} hit  skill {2} cast, {3} hit",
                    _player.BasicShotHits, _player.BasicShotsFired, _player.SkillShotsFired, _player.SkillShotHits));
                lines += 5;
            }
            foreach (Damageable d in Damageable.All)
            {
                _text.AppendLine(string.Format(s_Inv, "{0} {1}/{2}", d.name, d.CurrentHealth, d.MaxHealth));
                lines++;
            }
            _text.Append(string.Format(s_Inv, "frame {0}", Time.frameCount));

            GUI.Box(new Rect(10, 10, 520, 12 + lines * 20), _text.ToString(), _style);
        }
    }
}
