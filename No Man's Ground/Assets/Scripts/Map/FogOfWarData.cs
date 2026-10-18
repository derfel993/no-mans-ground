using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoMansGround.Map
{
    public sealed class FogOfWarData
    {
        private readonly int width;
        private readonly int height;
        private readonly bool[] explored;
        private readonly bool[] visible;
        public bool Enabled { get; private set; }

        public FogOfWarData(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width;
            this.height = height;
            explored = new bool[checked(width * height)];
            visible = new bool[explored.Length];
        }

        public bool IsExplored(Vector2Int cell)
        {
            return Contains(cell) && (!Enabled || explored[cell.y * width + cell.x]);
        }

        public bool IsVisible(Vector2Int cell)
        {
            return Contains(cell) && (!Enabled || visible[cell.y * width + cell.x]);
        }

        public void Enable()
        {
            Enabled = true;
            Array.Clear(explored, 0, explored.Length);
            Array.Clear(visible, 0, visible.Length);
        }

        public void Disable()
        {
            Enabled = false;
        }

        public void Update(MapData map, IEnumerable<Vector2Int> observers, int radius)
        {
            if (map == null || map.Width != width || map.Height != height)
                throw new ArgumentException("Fog dimensions must match the map.", nameof(map));
            if (observers == null)
                throw new ArgumentNullException(nameof(observers));
            if (radius < 1 || radius > 64)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!Enabled)
                return;
            Array.Clear(visible, 0, visible.Length);
            foreach (Vector2Int origin in observers)
            {
                if (!Contains(origin))
                    continue;
                for (int y = Math.Max(0, origin.y - radius); y <= Math.Min(height - 1, origin.y + radius); y++)
                    for (int x = Math.Max(0, origin.x - radius); x <= Math.Min(width - 1, origin.x + radius); x++)
                    {
                        int dx = x - origin.x;
                        int dy = y - origin.y;
                        var cell = new Vector2Int(x, y);
                        if (dx * dx + dy * dy > radius * radius || !HasSight(map, origin, cell))
                            continue;
                        int index = y * width + x;
                        visible[index] = true;
                        explored[index] = true;
                    }
            }
        }

        private bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }

        private static bool HasSight(MapData map, Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = -Mathf.Abs(to.y - from.y);
            int stepX = from.x < to.x ? 1 : -1;
            int stepY = from.y < to.y ? 1 : -1;
            int error = dx + dy;
            while (from != to)
            {
                int twice = 2 * error;
                Vector2Int next = from;
                if (twice >= dy) { error += dy; next.x += stepX; }
                if (twice <= dx) { error += dx; next.y += stepY; }
                if (next.x != from.x && next.y != from.y
                    && (map.BlocksSight(new Vector2Int(next.x, from.y))
                        || map.BlocksSight(new Vector2Int(from.x, next.y))))
                    return false;
                from = next;
                if (from == to)
                    return true;
                if (map.BlocksSight(from))
                    return false;
            }
            return true;
        }
    }
}