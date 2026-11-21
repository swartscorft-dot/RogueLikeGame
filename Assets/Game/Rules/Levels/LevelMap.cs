using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// A level drawn as characters, for logs, test failures and reports. North is up, as on the screen. Each cell is
    /// two characters across and two lines down, so walls get characters of their own: walls are '|' and '-' with '+'
    /// at their corners, a door is a gap in a wall, a room shows the letter of its kind (E entrance, C combat, T
    /// treasure, X exit; work order 012) and its place in the run in its north-west corner, the player's start is '@'
    /// and an enemy point the letter of what stands there (work order 008, <see cref="EnemyGroup.Letter"/>): b brute,
    /// s swarmer, p a pack of swarmers, h shooter, g guard; since work order 012 H a line of shooters, B a line of
    /// brutes and G the act boss; since work order 017 the elite room is L and its elite e; since work order 027 c a caster. A prop (work order 009) is its model's character (<see cref="PropSize.Letter"/>):
    /// # pillar, % broken pillar, = sarcophagus, : rubble, &amp; statue, ! grave marker, u urns, ~ bone pile, * brazier,
    /// $ chest, A altar; since work order 028 [ coffin, ^ skull pile, i candelabra, Y bone throne, k bookshelf, q cage,
    /// o basin; on a blank only, so a point or a wall hides one that falls on it, and one at a wall is drawn a
    /// character inside it. The map is two characters to a cell, 2 m each: it shows roughly where, not the shapes.
    /// The first line says the seed, the version and the size, and since work order 028 its legend: each themed room's
    /// label and theme (<see cref="RoomThemes.Word"/>), none when no room has one.
    /// Work order 051: the walls are drawn by <see cref="LevelLayout.RoomAtCell"/>, so a room's carved outline shows by
    /// itself: a cell walled round with nothing inside is solid. The room's label stands in its first open cell from the
    /// north-west.
    /// Work order 058: after the themes, the legend of the rooms stamped from a template (<see cref="RoomTemplates.Legend"/>),
    /// none when no room is. Work order 059: a passage is a strip one cell wide between two rooms, labelled P alone in its first
    /// open cell from the north-west.
    /// </summary>
    public static class LevelMap
    {
        public static string Draw(LevelLayout level)
        {
            CellRect box = level.Bounds();
            int cols = 2 * box.Width + 1, rows = 2 * box.Height + 1;
            var grid = new char[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++) grid[r, c] = ' ';

            // Text row and column of a point on the doubled grid, where (2x + 1, 2y + 1) is the middle of cell (x, y).
            void Put(int gx, int gy, char ch)
            {
                if (gx < 0 || gy < 0 || gx >= cols || gy >= rows) return;
                grid[rows - 1 - gy, gx] = ch;
            }
            int Owner(int x, int y)
            {
                LevelRoom room = level.RoomAtCell(x, y);
                return room != null ? room.Order : -1;
            }
            bool IsDoor(bool vertical, int line, int cell)
            {
                foreach (LevelDoor d in level.Doors)
                    if (d.Vertical == vertical && d.Line == line && cell >= d.From && cell < d.From + d.Width) return true;
                return false;
            }
            // A wall on the edge between two cells: they belong to different rooms, at least one is a room, no door.
            bool VerticalWall(int line, int y) => Owner(line - 1, y) != Owner(line, y) && !IsDoor(true, line, y);
            bool HorizontalWall(int line, int x) => Owner(x, line - 1) != Owner(x, line) && !IsDoor(false, line, x);

            for (int x = box.X; x <= box.Right; x++)
                for (int y = box.Y; y < box.Top; y++)
                    if (VerticalWall(x, y)) Put(2 * (x - box.X), 2 * (y - box.Y) + 1, '|');
            for (int y = box.Y; y <= box.Top; y++)
                for (int x = box.X; x < box.Right; x++)
                    if (HorizontalWall(y, x)) Put(2 * (x - box.X) + 1, 2 * (y - box.Y), '-');
            for (int x = box.X; x <= box.Right; x++)
                for (int y = box.Y; y <= box.Top; y++)
                    if (VerticalWall(x, y) || VerticalWall(x, y - 1) || HorizontalWall(y, x) || HorizontalWall(y, x - 1))
                        Put(2 * (x - box.X), 2 * (y - box.Y), '+');

            double cs = level.CellSize;
            foreach (LevelRoom room in level.Rooms)
            {
                // Work order 059: a passage, a cell wide, shows its letter alone, so it does not run over its walls.
                string label = room.Kind == RoomKind.Passage ? "P" : room.Letter + room.Order.ToString(CultureInfo.InvariantCulture);
                // In its north-west corner; work order 051: the first open cell of its northernmost row with one, west first.
                int lx = room.Rect.X, ly = room.Rect.Top - 1;
                for (int y = room.Rect.Top - 1, found = 0; y >= room.Rect.Y && found == 0; y--)
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        if (room.IsOpen(x, y))
                        {
                            lx = x;
                            ly = y;
                            found = 1;
                            break;
                        }
                for (int i = 0; i < label.Length; i++)
                    Put(2 * (lx - box.X) + 1 + i, 2 * (ly - box.Y) + 1, label[i]);
            }
            void Point(LevelPoint p, char ch) =>
                Put((int)Math.Round(2 * (p.X / cs - box.X), MidpointRounding.AwayFromZero),
                    (int)Math.Round(2 * (p.Y / cs - box.Y), MidpointRounding.AwayFromZero), ch);
            // Props (work order 009): inside their room's walls, on a blank only, under the points.
            foreach (LevelRoom room in level.Rooms)
                foreach (LevelProp prop in room.Props)
                {
                    int gx = (int)Math.Round(2 * (prop.Position.X / cs - box.X), MidpointRounding.AwayFromZero);
                    int gy = (int)Math.Round(2 * (prop.Position.Y / cs - box.Y), MidpointRounding.AwayFromZero);
                    gx = Math.Max(2 * (room.Rect.X - box.X) + 1, Math.Min(2 * (room.Rect.Right - box.X) - 1, gx));
                    gy = Math.Max(2 * (room.Rect.Y - box.Y) + 1, Math.Min(2 * (room.Rect.Top - box.Y) - 1, gy));
                    if (gx >= 0 && gy >= 0 && gx < cols && gy < rows && grid[rows - 1 - gy, gx] == ' ') Put(gx, gy, prop.Letter);
                }
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.PlayerStart.HasValue) Point(room.PlayerStart.Value, '@');
                for (int i = 0; i < room.EnemyPoints.Count; i++) Point(room.EnemyPoints[i], room.GroupAt(i).Letter);
            }

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "seed {0}  floor {5}  version {1}  {2} rooms  {6} enemies  {3:0} x {4:0} m\n",
                            level.SeedText, level.Version, level.Rooms.Count, box.Width * cs, box.Height * cs, level.Floor,
                            level.Rooms.Sum(r => r.EnemyCount));
            if (level.Rooms.Any(r => r.Theme != RoomTheme.None))
            {
                // Work order 028: the legend of the rooms' themes, on the first line (the lines under it are the map's).
                sb.Length--;
                sb.Append("  themes ").Append(string.Join(", ", level.Rooms.Where(r => r.Theme != RoomTheme.None)
                    .Select(r => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture) + " " + RoomThemes.Word(r.Theme)))).Append('\n');
            }
            string templates = RoomTemplates.Legend(level); // work order 058: the rooms stamped from a template, after the themes
            if (templates.Length > 0)
            {
                sb.Length--;
                sb.Append("  templates ").Append(templates).Append('\n');
            }
            for (int r = 0; r < rows; r++)
            {
                var line = new StringBuilder(cols);
                for (int c = 0; c < cols; c++) line.Append(grid[r, c]);
                sb.Append(line.ToString().TrimEnd()).Append('\n');
            }
            return sb.ToString();
        }
    }
}
