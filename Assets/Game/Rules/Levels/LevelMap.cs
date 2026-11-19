using System;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// A level drawn as characters, for logs, test failures and reports. North is up, as on the screen. Each cell is
    /// two characters across and two lines down, so walls get characters of their own: walls are '|' and '-' with '+'
    /// at their corners, a door is a gap in a wall, a room shows the letter of its kind (S start, C combat, R reward,
    /// B boss) and its place in the run in its north-west corner, the player's start is '@' and an enemy point '.'.
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
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.PlayerStart.HasValue) Point(room.PlayerStart.Value, '@');
                foreach (LevelPoint p in room.EnemyPoints) Point(p, '.');
            }

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "seed {0}  version {1}  {2} rooms  {3:0} x {4:0} m\n",
                            level.SeedText, level.Version, level.Rooms.Count, box.Width * cs, box.Height * cs);
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
