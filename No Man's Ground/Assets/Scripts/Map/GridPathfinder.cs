using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoMansGround.Map
{
    public sealed class GridPath
    {
        public int StartIndex { get; }
        public int Cost { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }

        internal GridPath(int startIndex, int cost, List<Vector2Int> cells)
        {
            StartIndex = startIndex;
            Cost = cost;
            Cells = cells.AsReadOnly();
        }
    }

    public static class GridPathfinder
    {
        // Reverse Dijkstra finds the nearest reachable start in a single search.
        // Costs are 10 for orthogonal steps and 14 for diagonal steps.
        public static GridPath FindNearest(MapData map, IReadOnlyList<Vector2Int> starts, Vector2Int destination)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (starts == null)
                throw new ArgumentNullException(nameof(starts));
            if (!map.IsWalkable(destination))
                return null;

            int count = checked(map.Width * map.Height);
            var distances = new int[count];
            var parents = new int[count];
            var candidates = new Dictionary<int, int>();
            for (int i = 0; i < count; i++)
            {
                distances[i] = int.MaxValue;
                parents[i] = -1;
            }
            for (int i = 0; i < starts.Count; i++)
            {
                if (!map.IsWalkable(starts[i]))
                    continue;
                int index = starts[i].y * map.Width + starts[i].x;
                if (!candidates.ContainsKey(index))
                    candidates.Add(index, i);
            }
            if (candidates.Count == 0)
                return null;

            int goal = destination.y * map.Width + destination.x;
            distances[goal] = 0;
            var frontier = new MinHeap();
            frontier.Push(goal, 0);
            while (frontier.Count > 0)
            {
                Entry current = frontier.Pop();
                if (current.Cost != distances[current.Index])
                    continue;
                if (candidates.TryGetValue(current.Index, out int startIndex))
                {
                    var path = new List<Vector2Int>();
                    for (int node = current.Index; node != -1; node = parents[node])
                        path.Add(new Vector2Int(node % map.Width, node / map.Width));
                    return new GridPath(startIndex, current.Cost, path);
                }

                var from = new Vector2Int(current.Index % map.Width, current.Index / map.Width);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var to = new Vector2Int(from.x + dx, from.y + dy);
                        if (!map.CanStep(from, to))
                            continue;
                        int next = to.y * map.Width + to.x;
                        int cost = current.Cost + (dx != 0 && dy != 0 ? 14 : 10);
                        if (cost >= distances[next])
                            continue;
                        distances[next] = cost;
                        parents[next] = current.Index;
                        frontier.Push(next, cost);
                    }
                }
            }
            return null;
        }

        private readonly struct Entry
        {
            public readonly int Index;
            public readonly int Cost;

            public Entry(int index, int cost)
            {
                Index = index;
                Cost = cost;
            }

            public bool Before(Entry other)
            {
                return Cost < other.Cost || (Cost == other.Cost && Index < other.Index);
            }
        }

        private sealed class MinHeap
        {
            private readonly List<Entry> entries = new List<Entry>();
            public int Count => entries.Count;

            public void Push(int index, int cost)
            {
                var entry = new Entry(index, cost);
                int position = entries.Count;
                entries.Add(entry);
                while (position > 0)
                {
                    int parent = (position - 1) / 2;
                    if (!entry.Before(entries[parent]))
                        break;
                    entries[position] = entries[parent];
                    position = parent;
                }
                entries[position] = entry;
            }

            public Entry Pop()
            {
                Entry first = entries[0];
                Entry last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0)
                    return first;
                int position = 0;
                while (position * 2 + 1 < entries.Count)
                {
                    int child = position * 2 + 1;
                    if (child + 1 < entries.Count && entries[child + 1].Before(entries[child]))
                        child++;
                    if (!entries[child].Before(last))
                        break;
                    entries[position] = entries[child];
                    position = child;
                }
                entries[position] = last;
                return first;
            }
        }
    }
}
