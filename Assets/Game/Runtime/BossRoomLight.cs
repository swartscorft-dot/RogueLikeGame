using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The throne room answers the boss's break (work order 106): the lights its room already has take one colour for the rest of
    /// the fight, through what each light already is (only its colour; its brightness, reach and flicker stay its own). No light is
    /// made. The lights of its room: the lamps of the lamplit light (<see cref="LamplitView"/>, "Lamp_kind_room_n"), the mood's own
    /// few (<see cref="RoomMood.RoomParts.Lights"/>) and the lanterns' (<see cref="CryptKitLights"/>), whichever the floor has. In the
    /// practice room (no floor) there is nothing to answer. Called once, at the break; it allocates then only.
    /// </summary>
    public static class BossRoomLight
    {
        /// <summary>Gives the lights of the boss's room this colour; returns how many there were.</summary>
        public static int Answer(EnemyBoss boss, Rgb colour)
        {
            if (boss == null || boss.InRoom == null) return 0;
            GeneratedLevel level = boss.GetComponentInParent<GeneratedLevel>();
            if (level == null) return 0;
            LevelRoom room = level.RoomAt(boss.Home);
            if (room == null) return 0;
            var c = new Color(colour.R, colour.G, colour.B, 1f);
            int order = room.Order, n = 0;
            foreach (Light l in Of(level, order))
            {
                l.color = c;
                n++;
            }
            return n;
        }

        /// <summary>The lights of the room with that place in the run, on this floor: lamplit lamps, the mood's lights, the lanterns' lights.</summary>
        public static List<Light> Of(GeneratedLevel level, int order)
        {
            var lights = new List<Light>();
            if (level == null) return lights;
            LamplitView view = level.GetComponent<LamplitView>();
            if (view != null && view.Root != null)
                foreach (Light l in view.Root.GetComponentsInChildren<Light>(true))
                    if (l != null && LampRoom(l.name) == order) lights.Add(l);
            RoomMood.RoomParts parts = level.Mood != null ? level.Mood.Of(order) : null;
            if (parts != null)
                foreach (Light l in parts.Lights)
                    if (l != null && !lights.Contains(l)) lights.Add(l);
            CryptKitLights kit = level.GetComponent<CryptKitLights>();
            if (kit != null && kit.ByRoom.TryGetValue(order, out List<Light> lanterns))
                foreach (Light l in lanterns)
                    if (l != null && !lights.Contains(l)) lights.Add(l);
            return lights;
        }

        /// <summary>The room of a lamplit lamp by its name ("Lamp_Brazier_3_12": room 3); -1 for anything else (a shot's flash).</summary>
        public static int LampRoom(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Lamp_", System.StringComparison.Ordinal)) return -1;
            string[] parts = name.Split('_');
            return parts.Length == 4 && int.TryParse(parts[2], out int room) ? room : -1;
        }
    }
}
