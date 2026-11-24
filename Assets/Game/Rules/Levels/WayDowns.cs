using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The exit rooms of their own in a run (work order 103; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24, its words
    /// "tronrommet bare i etasje 5 og egne utgangsrom i etasje 1–4"). A floor's exit room is drawn by the floor's seed, on the
    /// templates' stream as every room's is (<see cref="RoomTemplates.Draw"/>); a floor knows only its own seed and number, so
    /// a run tells each floor the room the floor before ended in (<see cref="Shown"/>, passed as
    /// <see cref="LevelLayout.ExitBefore"/>), and its exit room passes over that one while another of its own is left to draw
    /// (<see cref="Avoid"/>): with three rooms and two left after it, a run of four floors never shows the same one twice running.
    /// A floor built alone (outside a run, the benchmark's, F5's floor of a seed) is told none and draws from all three. With
    /// <see cref="LevelValues.ExitRoomsOfTheirOwn"/> off, and on floor 5 (the throne rooms), nothing here does anything.
    /// </summary>
    public static class WayDowns
    {
        /// <summary>
        /// The exit room of its own a floor ended in, as the next floor of its run is to be told: its template's name when it is
        /// one of the three (<see cref="ExitRooms.IsWayDown"/>) and was stamped as it is; null for any other (a throne room, a
        /// stamp that gave way to a drawn shape, none).
        /// </summary>
        public static string Shown(LevelLayout level)
        {
            LevelRoom exit = level?.ExitRoom;
            if (exit == null || exit.Shape != RoomShape.Template || !ExitRooms.IsWayDown(exit.Template?.Template)) return null;
            return exit.Template.Template.Name;
        }

        /// <summary>
        /// Takes the room named <paramref name="before"/> out of an exit room's options when at least one other of its own is left
        /// in them; otherwise leaves them as they are (where it cannot be helped, it is drawn again). Nothing with the switch off,
        /// on the act's last floor, or with no room before.
        /// </summary>
        public static void Avoid(List<RoomTemplate> options, string before, int floor, LevelValues v)
        {
            if (options == null || string.IsNullOrEmpty(before) || v == null || !v.ExitRoomsOfTheirOwn || floor >= v.FloorsPerAct) return;
            int others = 0;
            foreach (RoomTemplate t in options)
                if (ExitRooms.IsWayDown(t) && t.Name != before) others++;
            if (others > 0) options.RemoveAll(t => ExitRooms.IsWayDown(t) && t.Name == before);
        }
    }
}
