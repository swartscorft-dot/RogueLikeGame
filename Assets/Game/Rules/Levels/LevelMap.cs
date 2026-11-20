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
    /// brutes and G the act boss. A prop (work order 009) is its model's character (<see cref="PropSize.Letter"/>):
    /// # pillar, % broken pillar, = sarcophagus, : rubble, &amp; statue, ! grave marker, u urns, ~ bone pile, * brazier,
    /// $ chest, A altar; on a blank only, so a point or a wall hides one that falls on it, and one at a wall is drawn a
    /// character inside it. The map is two characters to a cell, 2 m each: it shows roughly where, not the shapes.
    /// The first line says the seed, the version and the size.
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
                string label = room.Letter + room.Order.ToString(CultureInfo.InvariantCulture);
                for (int i = 0; i < label.Length; i++)
                    Put(2 * (room.Rect.X - box.X) + 1 + i, 2 * (room.Rect.Top - 1 - box.Y) + 1, label[i]);
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
