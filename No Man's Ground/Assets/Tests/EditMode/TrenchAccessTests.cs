using NoMansGround.Map;
using NUnit.Framework;
using UnityEngine;

namespace NoMansGround.Tests
{
    public sealed class TrenchAccessTests
    {
        [Test]
        public void TrenchInteriorIsWalkableButGroundCrossingRequiresLadderOnThatCell()
        {
            var map = new MapData(6, 5);
            var entrance = new Vector2Int(2, 2);
            var interior = new Vector2Int(3, 2);
            map[2, 2] = map[3, 2] = TerrainKind.Trench;
            Assert.IsTrue(map.IsWalkable(entrance));
            Assert.IsTrue(map.CanStep(entrance, interior));
            Assert.IsFalse(map.CanStep(new Vector2Int(2, 1), entrance));
            Assert.IsFalse(map.CanStep(entrance, new Vector2Int(2, 1)));
            Assert.IsNull(GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, interior));
            map.AddLadder(entrance);
            Assert.IsTrue(map.HasLadder(entrance));
            Assert.IsTrue(map.CanStep(new Vector2Int(2, 1), entrance));
            Assert.IsTrue(map.CanStep(entrance, new Vector2Int(2, 1)));
            Assert.IsFalse(map.CanStep(interior, new Vector2Int(3, 1)));
            GridPath entry = GridPathfinder.FindNearest(map, new[] { Vector2Int.zero }, interior);
            Assert.IsNotNull(entry);
            CollectionAssert.Contains(entry.Cells, entrance);
            GridPath exit = GridPathfinder.FindNearest(map, new[] { interior }, Vector2Int.zero);
            Assert.IsNotNull(exit);
            CollectionAssert.Contains(exit.Cells, entrance);
            foreach (var path in new[] { entry, exit })
                for (int i = 1; i < path.Cells.Count; i++)
                    Assert.IsTrue(map.CanStep(path.Cells[i - 1], path.Cells[i]));
        }

        [Test]
        public void DiagonalsCannotBypassTrenchWallsEvenAtLadders()
        {
            var map = new MapData(4, 4);
            map[1, 1] = TerrainKind.Trench;
            Assert.IsFalse(map.CanStep(Vector2Int.zero, Vector2Int.one));
            map.AddLadder(Vector2Int.one);
            Assert.IsFalse(map.CanStep(Vector2Int.zero, Vector2Int.one));
            Assert.IsFalse(map.CanStep(Vector2Int.one, new Vector2Int(2, 2)));
            Assert.IsFalse(map.CanStep(new Vector2Int(1, 0), new Vector2Int(2, 1)));
            map[2, 1] = map[1, 2] = map[2, 2] = TerrainKind.Trench;
            Assert.IsTrue(map.CanStep(Vector2Int.one, new Vector2Int(2, 2)));
            map.SetWalkable(new Vector2Int(2, 1), false);
            Assert.IsFalse(map.CanStep(Vector2Int.one, new Vector2Int(2, 2)));
        }

        [Test]
        public void InvalidLaddersAreRejectedAndTerrainReplacementClearsLadderState()
        {
            var map = new MapData(3, 3);
            Assert.Throws<System.InvalidOperationException>(() => map.AddLadder(Vector2Int.zero));
            Assert.Throws<System.InvalidOperationException>(() => map.AddLadder(new Vector2Int(-1, 0)));
            map[1, 1] = TerrainKind.Trench;
            map.SetWalkable(Vector2Int.one, false);
            Assert.Throws<System.InvalidOperationException>(() => map.AddLadder(Vector2Int.one));
            map.SetWalkable(Vector2Int.one, true);
            map.AddLadder(Vector2Int.one);
            Assert.Throws<System.InvalidOperationException>(() => map.AddLadder(Vector2Int.one));
            map.SetWalkable(Vector2Int.one, false);
            Assert.IsFalse(map.CanStep(Vector2Int.one, Vector2Int.up));
            map[1, 1] = TerrainKind.Dirt;
            Assert.IsFalse(map.HasLadder(Vector2Int.one));
            map[1, 1] = TerrainKind.Trench;
            Assert.IsFalse(map.HasLadder(Vector2Int.one));
        }
    }
}
