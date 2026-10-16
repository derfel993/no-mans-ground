using System;
using System.Collections.Generic;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEngine;

namespace NoMansGround.Tests
{
    public sealed class GridPathfinderTests
    {
        [Test]
        public void EightDirectionsUseShortestCostWithoutCuttingCorners()
        {
            var map = new MapData(6, 6);
            GridPath diagonal = GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, new Vector2Int(3, 3));
            Assert.AreEqual(42, diagonal.Cost);
            Assert.AreEqual(4, diagonal.Cells.Count);
            map.SetWalkable(new Vector2Int(1, 0), false);
            GridPath detour = GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, new Vector2Int(3, 3));
            Assert.AreEqual(48, detour.Cost);
            AssertPath(map, detour, Vector2Int.zero, new Vector2Int(3, 3));
            Assert.AreEqual(Vector2Int.up, detour.Cells[1]);
            map.SetWalkable(new Vector2Int(0, 1), false);
            Assert.IsNull(GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, Vector2Int.one));
        }

        [Test]
        public void NearestCandidateIsChosenByReachablePathNotStraightLine()
        {
            var map = new MapData(7, 7);
            for (int y = 0; y < 6; y++)
                map.SetWalkable(new Vector2Int(3, y), false);
            var starts = new[] { new Vector2Int(2, 0), new Vector2Int(6, 4) };
            var destination = new Vector2Int(4, 0);
            GridPath path = GridPathfinder.FindNearest(map, starts, destination);
            Assert.AreEqual(1, path.StartIndex);
            Assert.AreEqual(48, path.Cost);
            AssertPath(map, path, starts[1], destination);
        }

        [Test]
        public void InvalidEndpointsUnreachableAreasAndSameCellAreHandled()
        {
            var map = new MapData(3, 3);
            Assert.IsNull(GridPathfinder.FindNearest(map, Array.Empty<Vector2Int>(), Vector2Int.zero));
            Assert.IsNull(GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, new Vector2Int(-1, 0)));
            map.SetWalkable(Vector2Int.one, false);
            Assert.IsNull(GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, Vector2Int.one));
            Assert.IsNull(GridPathfinder.FindNearest(map, new[] { Vector2Int.one }, Vector2Int.zero));
            GridPath same = GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, Vector2Int.zero);
            Assert.Zero(same.Cost);
            Assert.AreEqual(1, same.Cells.Count);
            Assert.Throws<ArgumentNullException>(() => GridPathfinder.FindNearest(null, new[] { Vector2Int.zero }, Vector2Int.zero));
            Assert.Throws<ArgumentNullException>(() => GridPathfinder.FindNearest(map, null, Vector2Int.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SetWalkable(new Vector2Int(3, 0), false));
        }

        [Test]
        public void HeapSearchMatchesIndependentReferenceOnSeededObstacleMaps()
        {
            var random = new System.Random(1918);
            for (int sample = 0; sample < 20; sample++)
            {
                var map = new MapData(10, 9);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        map.SetWalkable(new Vector2Int(x, y), random.NextDouble() > 0.25);
                var starts = new[] { Vector2Int.zero, new Vector2Int(9, 0), new Vector2Int(0, 8) };
                var goal = new Vector2Int(9, 8);
                foreach (Vector2Int start in starts)
                    map.SetWalkable(start, true);
                map.SetWalkable(goal, true);
                GridPath path = GridPathfinder.FindNearest(map, starts, goal);
                int minimum = int.MaxValue;
                foreach (Vector2Int start in starts)
                    minimum = Mathf.Min(minimum, ReferenceCost(map, start, goal));
                if (minimum == int.MaxValue)
                    Assert.IsNull(path);
                else
                {
                    Assert.IsNotNull(path);
                    Assert.AreEqual(minimum, path.Cost);
                    AssertPath(map, path, starts[path.StartIndex], goal);
                }
            }
        }

        [TestCase(1)]
        [TestCase(128)]
        [TestCase(512)]
        public void SupportedMapSizesProduceCompletePaths(int size)
        {
            var map = new MapData(size, size);
            var goal = new Vector2Int(size - 1, size - 1);
            GridPath path = GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, goal);
            Assert.IsNotNull(path);
            Assert.AreEqual((size - 1) * 14, path.Cost);
            Assert.AreEqual(size, path.Cells.Count);
            AssertPath(map, path, Vector2Int.zero, goal);
        }

        private static int ReferenceCost(MapData map, Vector2Int start, Vector2Int goal)
        {
            var pending = new List<Vector2Int> { start };
            var costs = new Dictionary<Vector2Int, int> { { start, 0 } };
            var visited = new HashSet<Vector2Int>();
            while (pending.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < pending.Count; i++)
                    if (costs[pending[i]] < costs[pending[best]])
                        best = i;
                Vector2Int current = pending[best];
                pending.RemoveAt(best);
                if (!visited.Add(current))
                    continue;
                if (current == goal)
                    return costs[current];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = current + new Vector2Int(dx, dy);
                        if (!map.CanStep(current, next) || visited.Contains(next))
                            continue;
                        int cost = costs[current] + (dx != 0 && dy != 0 ? 14 : 10);
                        if (costs.TryGetValue(next, out int previous) && previous <= cost)
                            continue;
                        costs[next] = cost;
                        pending.Add(next);
                    }
            }
            return int.MaxValue;
        }

        private static void AssertPath(MapData map, GridPath path, Vector2Int start, Vector2Int goal)
        {
            Assert.AreEqual(start, path.Cells[0]);
            Assert.AreEqual(goal, path.Cells[path.Cells.Count - 1]);
            int cost = 0;
            for (int i = 1; i < path.Cells.Count; i++)
            {
                Assert.IsTrue(map.CanStep(path.Cells[i - 1], path.Cells[i]));
                Vector2Int delta = path.Cells[i] - path.Cells[i - 1];
                cost += delta.x != 0 && delta.y != 0 ? 14 : 10;
            }
            Assert.AreEqual(cost, path.Cost);
        }
    }
}
