using System;
using UnityEngine;

namespace NoMansGround.Map
{
    public enum TerrainKind
    {
        Dirt,
        Grass,
        Mud,
        Trench,
        Road
    }

    public sealed class MapData
    {
        private readonly TerrainKind[] cells;
        private readonly bool[] blocked;
        private readonly bool[] ladders;

        public int Width { get; }
        public int Height { get; }

        public MapData(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be positive.");

            Width = width;
            Height = height;
            cells = new TerrainKind[checked(width * height)];
            blocked = new bool[cells.Length];
            ladders = new bool[cells.Length];
        }

        public TerrainKind this[int x, int y]
        {
            get => cells[IndexOf(x, y)];
            set
            {
                int index = IndexOf(x, y);
                cells[index] = value;
                if (value != TerrainKind.Trench)
                    ladders[index] = false;
            }
        }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return Contains(cell) && !blocked[cell.y * Width + cell.x];
        }

        public void SetWalkable(Vector2Int cell, bool walkable)
        {
            blocked[IndexOf(cell.x, cell.y)] = !walkable;
        }

        public bool IsTrench(Vector2Int cell)
        {
            return Contains(cell) && this[cell.x, cell.y] == TerrainKind.Trench;
        }

        public bool HasLadder(Vector2Int cell)
        {
            return IsTrench(cell) && ladders[cell.y * Width + cell.x];
        }

        public void AddLadder(Vector2Int cell)
        {
            if (!IsTrench(cell) || !IsWalkable(cell))
                throw new InvalidOperationException("A ladder requires a walkable trench cell.");
            if (HasLadder(cell))
                throw new InvalidOperationException("This trench already has a ladder.");
            ladders[IndexOf(cell.x, cell.y)] = true;
        }

        public bool CanStep(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (!IsWalkable(from) || !IsWalkable(to) || Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1
                || (dx == 0 && dy == 0))
                return false;
            bool fromTrench = IsTrench(from);
            bool toTrench = IsTrench(to);
            if (dx == 0 || dy == 0)
                return fromTrench == toTrench || HasLadder(fromTrench ? from : to);
            var sideX = new Vector2Int(from.x + dx, from.y);
            var sideY = new Vector2Int(from.x, from.y + dy);
            return fromTrench == toTrench && IsWalkable(sideX) && IsWalkable(sideY)
                && IsTrench(sideX) == fromTrench && IsTrench(sideY) == fromTrench;
        }

        private int IndexOf(int x, int y)
        {
            if (!Contains(new Vector2Int(x, y)))
                throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the map.");

            return y * Width + x;
        }
    }
}
