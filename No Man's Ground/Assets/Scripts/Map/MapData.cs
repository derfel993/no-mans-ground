using System;
using UnityEngine;

namespace NoMansGround.Map
{
    public enum TerrainKind
    {
        Dirt,
        Grass,
        Mud
    }

    public sealed class MapData
    {
        private readonly TerrainKind[] cells;

        public int Width { get; }
        public int Height { get; }

        public MapData(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be positive.");

            Width = width;
            Height = height;
            cells = new TerrainKind[checked(width * height)];
        }

        public TerrainKind this[int x, int y]
        {
            get => cells[IndexOf(x, y)];
            set => cells[IndexOf(x, y)] = value;
        }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        private int IndexOf(int x, int y)
        {
            if (!Contains(new Vector2Int(x, y)))
                throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the map.");

            return y * Width + x;
        }
    }
}
